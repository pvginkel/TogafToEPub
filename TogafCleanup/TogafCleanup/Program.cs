using System.Drawing;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

var input = args[0];
var output = args[1];

Directory.CreateDirectory(output);

WriteScript();
RewriteFiles();

void WriteScript()
{
    using var writer = File.CreateText(Path.Combine(output, "paths.txt"));

    var index = ParseIndex();
    var sectionHeaderIndex = 0;

    foreach (var section in index)
    {
        foreach (var path in section.Paths)
        {
            var pos = path.Path.LastIndexOf('/');
            var subPath = path.Path.Substring(0, pos);

            Directory.CreateDirectory(Path.Combine(output, subPath));

            var sectionHeaderFileName = Path.Join(subPath, $"sectionheader{sectionHeaderIndex++}.html");

            File.WriteAllText(
                Path.Combine(output, sectionHeaderFileName),
                $"""
                <html>
                    <body>
                        <h1>{HtmlEntity.Entitize(path.Title)}</h1>
                    </body>
                </html>
                """
            );

            writer.Write(sectionHeaderFileName);
            writer.Write(" ");

            var indexPath = Path.Combine(input, subPath, "index.html");
            if (File.Exists(indexPath))
            {
                var doc = new HtmlDocument();
                doc.Load(indexPath);

                var seen = new HashSet<string>();

                foreach (var anchor in doc.GetElementbyId("toc").DescendantsAndSelf().Where(p => p.Name == "a"))
                {
                    if (anchor.InnerText.Length > 0)
                    {
                        var href = anchor.GetAttributeValue("href", null);
                        if (href.Contains("..") || href.Contains("://") || href.Contains("gsearch.html"))
                            continue;
                        
                        var anchorPath = href.Split('#')[0];

                        if (seen.Add(anchorPath))
                        {
                            writer.Write(Path.Join(subPath, anchorPath));
                            writer.Write(" ");
                        }
                    }
                }
            }
            else
            {
                writer.Write(path.Path);
                writer.Write(" ");
            }
        }
    }
}

List<(string TocTitle, List<(string Path, string Title)> Paths)> ParseIndex()
{
    var doc = new HtmlDocument();
    doc.Load(Path.Combine(input, "index.html"));

    var result = new List<(string, List<(string, string)>)>();

    var toctitle = default(string);

    foreach (var el in doc.GetElementbyId("toc").ChildNodes)
    {
        if (el.Id == "toctitle")
            toctitle = Cleanup(el.InnerText);

        if (el.Name == "ul")
        {
            var paths = new List<(string, string)>();

            foreach (var item in el.DescendantsAndSelf().Where(p => p.Name == "li"))
            {
                var anchor = item.DescendantsAndSelf().Single(p => p.Name == "a");
                var path = anchor.GetAttributeValue("href", null);

                // Fixups.
                if (path == "architecture-maturity-models.html")
                    path = "architecture-maturity-models/index.html";

                var title = Cleanup(anchor.InnerText);

                if (!path.Contains("://"))
                    paths.Add((path, title));
            }

            if (paths.Count > 0)
                result.Add((toctitle, paths));
        }
    }

    return result;
}

void RewriteFiles()
{
    var ids = new HashSet<string>();

    foreach (var file in Directory.GetFiles(input, "*", SearchOption.AllDirectories))
    {
        Console.WriteLine(file.Substring(input.Length));

        var target = output + file.Substring(input.Length);

        Directory.CreateDirectory(Path.GetDirectoryName(target));

        if (Path.GetExtension(file) == ".html")
        {
            var doc = new HtmlDocument();
            doc.Load(file);

            // Remove title elements. It's better to make this explicit when
            // calling pandoc.
            foreach (var title in doc.DocumentNode.DescendantsAndSelf("title").ToList())
            {
                title.Remove();
            }

            // Fix the content type.
            foreach (var tag in doc.DocumentNode.DescendantsAndSelf("meta"))
            {
                if (tag.GetAttributeValue("http-equiv", null) == "Content-Type")
                {
                    tag.SetAttributeValue(
                        "content",
                        tag.GetAttributeValue("content", "").Replace("iso-8859-1", "utf-8")
                    );
                }
            }

            // Remove the header and toc.
            doc.GetElementbyId("toc")?.Remove();
            doc.GetElementbyId("header")?.Remove();

            // Remove chapter toc.
            var content = doc.GetElementbyId("content");
            if (content != null)
            {
                var inToc = false;
                foreach (var el in content.ChildNodes.ToList())
                {
                    if (el is HtmlCommentNode cn && cn.Comment.Contains("chapter toc start"))
                        inToc = true;
                    if (inToc)
                        el.Remove();
                    if (el is HtmlCommentNode cn1 && cn1.Comment.Contains("chapter toc end"))
                        inToc = false;
                }
            }

            // Remove footer.
            var returnToTop = doc
                .DocumentNode.DescendantsAndSelf()
                .SingleOrDefault(p => p.Name == "div" && p.HasClass("returntotop"));
            
            if (returnToTop == null)
            {
                returnToTop = doc
                    .DocumentNode.DescendantsAndSelf()
                    .SingleOrDefault(p => p.Name == "p" && p.InnerText == "return to top of page");
            }

            if (returnToTop != null)
            {
                var hadEl = false;
                foreach (var el in returnToTop.ParentNode.ChildNodes.ToList())
                {
                    if (el == returnToTop)
                        hadEl = true;
                    if (hadEl)
                        el.Remove();
                }
            }

            // Remove the content div.
            if (content != null)
            {
                content.ParentNode.AppendChildren(content.ChildNodes);
                content.Remove();
            }

            // Renumber headers.
            if (doc.DocumentNode.DescendantsAndSelf().FirstOrDefault(p => p.Name == "h1") != null)
            {
                foreach (var node in doc.DocumentNode.DescendantsAndSelf())
                {
                    var match = Regex.Match(node.Name, @"^h(\d+)$");
                    if (match.Success)
                        node.Name = $"h{int.Parse(match.Groups[1].Value) + 1}";
                }
            }

            // Ensure unique IDs.
            foreach (var el in doc.DocumentNode.DescendantsAndSelf())
            {
                var id = el.GetAttributeValue("id", null);
                if (id != null && !ids.Add(id))
                {
                    for (var i = 1; ; i++)
                    {
                        var newId = id + i;
                        if (ids.Add(newId))
                        {
                            el.SetAttributeValue("id", newId);
                            break;
                        }
                    }
                }
            }

            doc.Save(target);
        }
        else
        {
            File.Copy(file, target, true);
        }
    }
}

string Cleanup(string text)
{
    return Regex.Replace(HtmlEntity.DeEntitize(text), @"\s+", " ").Trim();
}
