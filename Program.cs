using Open.Text;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Linq;

// Read XML from Source List.xspf.
// Write XML to Destination List.xspf.
const string sourcePath = "Source List.xspf";
const string destinationPath = "Destination List.xspf";

// First read the source XML as a stream of lines and escape any tag content.
string sourceContent = LineTagFix().Replace(File.ReadAllText(sourcePath), m =>
{
	var indent = m.Groups["indent"].ValueSpan;
	var name = m.Groups["name"].ValueSpan;
	string value = System.Security.SecurityElement.Escape(m.Groups["value"].Value);

	return $"{indent}<{name}>{value}</{name}>";
});

var xIn = XDocument.Parse(sourceContent);

var artists = new HashSet<string>().GetAlternateLookup<ReadOnlySpan<char>>();
var paths = new HashSet<string>().GetAlternateLookup<ReadOnlySpan<char>>();

// Replicate the parent nodes of the source XML to the destination XML.
var xOut = new XDocument();
Debug.Assert(xIn.Root is not null);
var xRoot = new XElement(xIn.Root.Name);
xOut.Add(xRoot);

int sourceTrackCount = 0;
int destTrackCount = 0;

// Include all the child nodes, but iterate through the trackList node.
foreach (var xNode in xIn.Root.Elements())
{
	string nodeName = xNode.Name.LocalName;
	if (nodeName != "trackList")
	{
		xRoot.Add(new XElement(xNode.Name, xNode.Value));
		continue;
	}

	var xTrackList = new XElement(xNode.Name);
	xRoot.Add(xTrackList);

	foreach (var xTrack in xNode.Elements())
	{
		sourceTrackCount++;

		// Read each <track> and catalog each artist (<creator> separated by semi-colons.).
		// If the artist is not already in the list, add the node to the XML output.
		var artistNode = xTrack.Elements().First(n => n.Name.LocalName == "creator");
		string artist = artistNode.Value;
		// split the artists by semi-colons and trim.
		// if any of them have already been added, skip the track.
		if (artist.SplitAsSegments(';').Any(a => !artists.Add(a.Trim())))
		{
			continue;
		}

		var locationNode = xTrack.Elements().First(n => n.Name.LocalName == "location");
		string location = locationNode.Value;
		// Isolate the parent path from the file and use that as another filter.
		// Use smart span based path parsing to get the parent path.
		int lastSlash = location.LastIndexOf('\\');
		var folderPath = location.AsSpan(0, lastSlash);
		if (!paths.Add(folderPath))
		{
			continue;
		}

		// Add the node to the destinatino XML ensuring the child nodes are copied.
		destTrackCount++;
		xTrackList.Add(xTrack);
		// break on the first for debugging.
	}
}

// Write the destination XML.
xOut.Save(destinationPath);

Console.WriteLine("Track Count: {0} => {1}", sourceTrackCount, destTrackCount);

partial class Program
{
	[GeneratedRegex(@"^(?<indent>\s+)<(?<name>location|creator|title|image|album|annotation)>(?<value>.+?)</?\2>\s*$", RegexOptions.Singleline | RegexOptions.Multiline)]
	private static partial Regex LineTagFix();
}