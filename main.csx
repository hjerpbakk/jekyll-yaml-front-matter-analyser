#!/usr/bin/env dotnet-script
#r "nuget: YamlDotNet, 6.0.0"

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

if (Args.Count != 1) {
    Console.WriteLine("Please provide the location of a Jekyll repository as an argument. Example: dotnet script main.csx -- [path_to_root_jekyll_folder]");
    return;
}

var whitelistedFiles = new string[0];
var whitelistPath = Path.Combine(Args[0], ".frontmatterignore");
if (File.Exists(whitelistPath)) {
    whitelistedFiles = File.ReadAllLines(whitelistPath);
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
    if (whitelistedFiles.Contains(postFilename)) {
        continue;
    }

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
    public List<string> ignore { get; set; }

    public List<string> Verify(Tag[] availableTags, string rootPath) {
        var errors = new List<string>();
        // categories
        if (categories == null || categories.Count == 0 || !categories.Contains("blog")) {
            errors.Add(Errors.CA0001);
        } else if (categories.Contains("link")) {
            if (string.IsNullOrEmpty(link)) {
                errors.Add(Errors.CA0002);
            }
        }

        // layout
        if (string.IsNullOrEmpty(layout) || layout != "post") {
            errors.Add(Errors.LA0001);
        }

        // title
        if (string.IsNullOrEmpty(title)) {
            errors.Add(Errors.TI0001);
        } else if (title.Contains("TODO", StringComparison.InvariantCultureIgnoreCase)) {
            errors.Add(Errors.TI0002);
        }

        // meta_description
        if (string.IsNullOrEmpty(meta_description)) {
            errors.Add(Errors.DE0001);
        } else if (meta_description.Contains("TODO", StringComparison.InvariantCultureIgnoreCase)) {
            errors.Add(Errors.DE0002);
        }

        // date
        if (date == DateTime.MinValue) {
            errors.Add(Errors.DA0001);
        } else if (date > DateTime.Now) {
            errors.Add(Errors.DA0002);
        }

        // image
        if (string.IsNullOrEmpty(image)) {
            errors.Add(Errors.IM0001);
        } else {
            var imagePathPart = image.Remove(0, 1);
            var imagePath = Path.GetFullPath(Path.Combine(rootPath, imagePathPart));
            if (!File.Exists(imagePath)) {
                errors.Add(Errors.IM0002);
            }
        }

        // tags
        if (tags == null || tags.Count == 0) {
            errors.Add(Errors.TA0001);
        } else {
            foreach (var tag in tags) {
                if (!availableTags.Any(t => t.slug == tag)) {
                    errors.Add(string.Format(Errors.TA0002, tag));
                }
            }
        }      
        
        if (ignore != null) {
            foreach (var ruleToIgnore in ignore) {
                for (int i = 0; i < errors.Count; i++) {
                    string error = errors[i];
                    if (error.Contains(ruleToIgnore)) {
                        errors.Remove(error);
                        i--;
                    }
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

struct Errors {
    public const string CA0001 = "\"categories\" must contain the value: blog (" + nameof(CA0001) + ")";
    public const string CA0002 = "When \"categories\" contains \"link\", a \"link\" with an url must exist in the front matter (" + nameof(CA0002) + ")";

    public const string LA0001 = "\"layout\" must have the value: post (" + nameof(LA0001) + ")";

    public const string TI0001 = "\"title\" is missing (" + nameof(TI0001) + ")";
    public const string TI0002 = "\"title\" cannot contain: TODO (" + nameof(TI0002) + ")";

    public const string DE0001 = "\"meta_description\" is missing (" + nameof(DE0001) + ")";
    public const string DE0002 = "\"meta_description\" cannot contain: TODO (" + nameof(DE0002) + ")";

    public const string DA0001 = "\"date\" is missing (" + nameof(DA0001) + ")";
    public const string DA0002 = "\"date\" was in the future (" + nameof(DA0002) + ")";

    public const string IM0001 = "\"image\" is missing (" + nameof(IM0001) + ")";
    public const string IM0002 = "\"image\" did not exist on disk (" + nameof(IM0002) + ")";

    public const string TA0001 = "Post must have at least one tag (" + nameof(TA0001) + ")";
    public const string TA0002 = "Could not find tag {0} in available tags (" + nameof(TA0002) + ")";
}