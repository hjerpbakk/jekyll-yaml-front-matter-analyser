#!/usr/bin/env dotnet-script
#r "nuget: YamlDotNet, 6.0.0"

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

if (Args.Count != 1) {
    Console.WriteLine("Please provide the location of a Jekyll repository as an argument. Example: dotnet script main.csx -- path");
    return;
}

var postPath = Path.GetFullPath(Path.Combine(Args[0], "_posts"));
var posts = "*.md;*.html".Split(';').SelectMany(g => Directory.GetFiles(postPath, g)).ToArray();
if (posts.Length == 0) {
    Console.WriteLine("Could not find a single post at " + postPath);
    return;
}

var tags = GetAvailableTags();
Console.WriteLine($"Analysing posts...{Environment.NewLine}");
var verificationResults = new Dictionary<string, List<string>>();

foreach (var post in posts) {
    var postFilename = Path.GetFileName(post);
    try {
        var frontMatterText = GetFrontMatterFromPost(post);
        var frontMatter = ParseFrontMatter(frontMatterText);
        verificationResults.Add(postFilename, frontMatter.Verify(tags, Args[0]));
    } catch (Exception exception) {
        verificationResults.Add(postFilename, new List<string>() { exception.Message });  
    }
}

var numberOfErrors = 0;
var defaultColor = Console.ForegroundColor;
foreach (var verificationResult in verificationResults) {
    if (verificationResult.Value.Count == 0) {
        continue;
    }

    Console.ForegroundColor = defaultColor;
    Console.WriteLine(verificationResult.Key);
    foreach (var error in verificationResult.Value) {
        numberOfErrors++;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(error);
    }
}

Console.ForegroundColor = defaultColor;
if (numberOfErrors > 0) {
    Console.WriteLine($"Found {numberOfErrors} errors 🤨");
    return 1;
} else {
    Console.WriteLine("No errors 😃");
    return 0;
}

string GetFrontMatterFromPost(string postPath) {
    var fullText = File.ReadAllText(postPath);
    var indexOfFirstLineBreak = fullText.IndexOf('\n');
    var indexOfFrontMatterEnd = fullText.IndexOf("---\n", indexOfFirstLineBreak, StringComparison.InvariantCulture);
    if (indexOfFrontMatterEnd == -1) {
        return null;
    }

    var frontMatterEnd = indexOfFrontMatterEnd + 3;
    var frontMatterText = fullText.Substring(0, frontMatterEnd).Trim('-');
    return frontMatterText;
}

FrontMatter ParseFrontMatter(string frontMatterText) {
    var deserializer = new DeserializerBuilder()
        .WithNamingConvention(new UnderscoredNamingConvention())
        .IgnoreUnmatchedProperties()
        .Build();
    return deserializer.Deserialize<FrontMatter>(frontMatterText);
}

Tag[] GetAvailableTags() {
    var tagsPath = Path.GetFullPath(Path.Combine(Args[0], "_my_tags"));
    var availableTags = Directory.EnumerateFileSystemEntries(tagsPath)
        .Where(f => f.EndsWith("md"))
        .Select(f => ParseTag(File.ReadAllText(f)));
    return availableTags.ToArray();
}

Tag ParseTag(string tagText) {
    var deserializer = new DeserializerBuilder()
        .WithNamingConvention(new UnderscoredNamingConvention())
        .IgnoreUnmatchedProperties()
        .Build();

    tagText = tagText.Trim('-');
    return deserializer.Deserialize<Tag>(tagText);
}

struct FrontMatter {
    public string title { get; set; }
    public List<string> tags { get; set; }
    public List<string> categories { get;set; }
    public string layout { get; set; }
    public string meta_description { get; set; }
    public DateTime date { get; set; }
    public string image { get; set; }
    public string link { get; set; }

    public List<string> Verify(Tag[] availableTags, string rootPath) {
        var errors = new List<string>();
        // categories
        if (categories == null || categories.Count == 0 || !categories.Contains("blog")) {
            errors.Add("categories must contain the value: blog");
        } else if (categories.Contains("link")) {
            if (string.IsNullOrEmpty(link)) {
                errors.Add("When categories contains \"link\", a valid link item is needed");
            }
        }

        // layout
        if (string.IsNullOrEmpty(layout) || layout != "post") {
            errors.Add("layout must have the value: post");
        }

        // title
        if (string.IsNullOrEmpty(title)) {
            errors.Add("title is missing");
        } else if (title.Contains("TODO", StringComparison.InvariantCultureIgnoreCase)) {
            errors.Add("title cannot contain TODO");
        }

        // meta_description
        if (string.IsNullOrEmpty(meta_description)) {
            errors.Add("meta_description is missing");
        } else if (meta_description.Contains("TODO", StringComparison.InvariantCultureIgnoreCase)) {
            errors.Add("meta_description cannot contain TODO");
        }

        // date
        if (date == DateTime.MinValue) {
            errors.Add("date must be specified");
        } else if (date > DateTime.Now) {
            errors.Add("date was in the future");
        }

        // image
        if (string.IsNullOrEmpty(image)) {
            errors.Add("image is missing");
        } else {
            var imagePathPart = image.Remove(0, 1);
            var imagePath = Path.GetFullPath(Path.Combine(rootPath, imagePathPart));
            if (!File.Exists(imagePath)) {
                errors.Add("image did not exist on disk");
            }
        }

        // tags
        if (tags == null || tags.Count == 0) {
            errors.Add("Post must have at least one tag");
        } else {
            foreach (var tag in tags) {
                if (!availableTags.Any(t => t.slug == tag)) {
                    errors.Add($"Could not find tag: {tag}");
                }
            }
        }      
        
        return errors;
    }
}

struct Tag {
    public string slug { get; set; }
    public string title { get; set; }
    public string hash_tag { get; set; }
}