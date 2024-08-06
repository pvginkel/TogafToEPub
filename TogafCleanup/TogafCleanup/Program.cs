using HtmlAgilityPack;

var input = args[0];
var output = args[1];

foreach (var file in Directory.GetFiles(input, "*", SearchOption.AllDirectories))
{
    Console.WriteLine(file.Substring(input.Length));

    var target = output + file.Substring(input.Length);

    Directory.CreateDirectory(Path.GetDirectoryName(target));

    if (Path.GetExtension(file) == ".html")
    {
        var doc = new HtmlDocument();
        doc.Load(file);

        // Rewrite the title.
        if (Path.GetFileName(file) == "index.html")
        {
            var title = doc.DocumentNode.DescendantsAndSelf("title").Single(p => p.Name == "title");
            var titleText = $"TOGAF® Standard 10 - {HtmlEntity.DeEntitize(title.InnerText)}";

            var toc = doc.GetElementbyId("toc");
            if (toc != null)
            {
                // Rewrite the title.
                var indexRef = toc.DescendantsAndSelf("a").SingleOrDefault(p => p.GetAttributeValue("href", null) == "index.html" && !string.IsNullOrWhiteSpace(p.InnerText));
                if (indexRef != null)
                    titleText = $"TOGAF® Standard 10 - {HtmlEntity.DeEntitize(indexRef.InnerText)}";
            }

            ((HtmlTextNode)title.FirstChild).Text = titleText;

            // Write an ePub metadata file that the pandoc generation script can pick up.

            File.WriteAllText(
                Path.Combine(Path.GetDirectoryName(target), "metadata.xml"),
                """
                <dc:language>en-us</dc:language> 
                <dc:creator>The Open Group</dc:creator> 
                <dc:rights>Copyright © 1999-2022 The Open Group, All Rights Reserved.</dc:rights>
                """
            );

            File.WriteAllText(Path.Combine(Path.GetDirectoryName(target), "title.txt"), titleText);
        }

        // Fix the content type.
        foreach (var tag in doc.DocumentNode.DescendantsAndSelf("meta"))
        {
            if (tag.GetAttributeValue("http-equiv", null) == "Content-Type")
                tag.SetAttributeValue("content", tag.GetAttributeValue("content", "").Replace("iso-8859-1", "utf-8"));
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
        var returnToTop = doc.DocumentNode.DescendantsAndSelf("div").SingleOrDefault(p => p.HasClass("returntotop"));
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

        doc.Save(target);
    }
    else
    {
        File.Copy(file, target, true);
    }
}
