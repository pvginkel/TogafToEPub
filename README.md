# TogafToEPub

This project converts the publicly available Togaf books into an EPub.

## Usage

The `run.sh` script downloads the book and does the conversion. It
requires Linux to run, and depends on `wget` and Docker. It has
dependencies on .NET and PanDoc but those are pulled in as Docker
containers.
