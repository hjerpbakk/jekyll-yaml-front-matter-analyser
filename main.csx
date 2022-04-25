#!/usr/bin/env dotnet-script
#r "nuget: YamlDotNet, 11.2.1"

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

Console.WriteLine($"Analysing posts...{Environment.NewLine}");
if (Args.Count != 1) {
    WriteError(Errors.JE0001);
    WriteErrorSummary(1);
    return 1;
}

var whitelistedFiles = new string[0];
var whitelistPath = Path.Combine(Args[0], ".frontmatterignore");
if (File.Exists(whitelistPath)) {
    whitelistedFiles = File.ReadAllLines(whitelistPath);
}

var postPath = Path.GetFullPath(Path.Combine(Args[0], "_posts"));
if (!Directory.Exists(postPath)) {
    WriteError(Errors.JE0002);
    WriteErrorSummary(1);
    return 1;
}

var posts = "*.md;*.html".Split(';').SelectMany(g => Directory.GetFiles(postPath, g)).ToArray();
if (posts.Length == 0) {
    WriteError(Errors.JE0003);
    WriteErrorSummary(1);
    return 1;
}

var tags = GetAvailableTags();
var verificationResults = new Dictionary<string, List<string>>();

(string postFilename, FrontMatter frontMatter, DateTime lastModified) newestPost = (null, null, DateTime.MinValue);
foreach (var post in posts) {
    var postFilename = Path.GetFileName(post);
    if (whitelistedFiles.Contains(postFilename)) {
        continue;
    }

    try {
        var frontMatter = FrontMatter.Parse(post);
        if (frontMatter == null) {
            verificationResults.Add(postFilename, new List<string>() { string.Format(Errors.DA0001, "") });
            continue;
        }
        var lastModified = frontMatter.last_modified_at.HasValue && frontMatter.last_modified_at.Value  > frontMatter.date ? frontMatter.last_modified_at.Value : frontMatter.date;
        if (lastModified > newestPost.lastModified) {
            newestPost = (postFilename, frontMatter, lastModified);
            if (newestPost.frontMatter == null) {
                WriteError(string.Format(Errors.DA0001, " from the newest post"));
                WriteErrorSummary(1);
                return 1;
            }
        }

        verificationResults.Add(postFilename, frontMatter.Verify(tags, Args[0]));
    } catch (Exception exception) {
        verificationResults.Add(postFilename, new List<string>() { exception.Message });  
    }
}

if (newestPost.frontMatter != null) {
    var lastModifiedErrors = newestPost.frontMatter.Verify(newestPost.lastModified, Args[0]);
    if (verificationResults.ContainsKey(newestPost.postFilename)) {
        verificationResults[newestPost.postFilename].AddRange(lastModifiedErrors);
    } else {
        verificationResults.Add(newestPost.postFilename, lastModifiedErrors);
    }
}

var numberOfErrors = 0;
foreach (var verificationResult in verificationResults) {
    if (verificationResult.Value.Count == 0) {
        continue;
    }

    Console.WriteLine(verificationResult.Key);
    foreach (var error in verificationResult.Value) {
        numberOfErrors++;
        WriteError(error);
    }
}

if (numberOfErrors > 0) {
    WriteErrorSummary(numberOfErrors);
    return 1;
} else {
    Console.WriteLine("No errors 😃");
    return 0;
}

Tag[] GetAvailableTags() {
    var tagsPath = Path.GetFullPath(Path.Combine(Args[0], "_my_tags"));
    if (!Directory.Exists(tagsPath)) {
        return new Tag[0];
    }

    var availableTags = Directory.EnumerateFileSystemEntries(tagsPath)
        .Where(f => f.EndsWith("md"))
        .Select(f => ParseTag(File.ReadAllText(f)));
    return availableTags.ToArray();
}

Tag ParseTag(string tagText) {
    var deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    tagText = tagText.Trim('-');
    return deserializer.Deserialize<Tag>(tagText);
}

sealed record FrontMatter {
    public string title { get; init; }
    public List<string> tags { get; init; }
    public List<string> categories { get; init; }
    public string layout { get; init; }
    public string meta_description { get; init; }
    public DateTime date { get; init; }
    public DateTime? last_modified_at { get; init; }
    public string image { get; init; }
    public string link { get; init; }
    public List<string> ignore { get; init; } = new List<string>();

    public static FrontMatter Parse(string postPath) {
        var frontMatterText = GetFrontMatterFromPost();
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        var frontMatter = deserializer.Deserialize<FrontMatter>(frontMatterText);
        return frontMatter;

        string GetFrontMatterFromPost() {
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
    }

    public List<string> Verify(DateTime lastModified, string rootPath) {
        var errors = new List<string>();
        var files = new [] { "index.html", "archives.html" };
        foreach (var file in files) {
            var filePath = Path.Combine(rootPath, file);
            if (!File.Exists(filePath)) {
                continue;
            }

            var frontMatter = Parse(filePath);
            if (frontMatter.ignore.Contains(nameof(Errors.DA0004))) {
                continue;
            }

            if (frontMatter.last_modified_at != lastModified) {
                errors.Add(string.Format(Errors.DA0004, file));
            }
        }

        return errors;
    }

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
        } else if (meta_description.Length < 25 || meta_description.Length > 160) {
            errors.Add(Errors.DE0003);
        }

        // date
        var now = DateTime.Now;
        if (date == DateTime.MinValue) {
            errors.Add(Errors.DA0001);
        } else if (date > now) {
            errors.Add(Errors.DA0002);
        } else if (last_modified_at > now) {
            errors.Add(Errors.DA0003);
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
        
        if (ignore.Count > 0) {
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

void WriteError(string error) {
    var defaultColor = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine(error);
    Console.ForegroundColor = defaultColor;
}

void WriteErrorSummary(int numberOfErrors) {
    Console.WriteLine($"{Environment.NewLine}Found {numberOfErrors} errors 🤨");
}

record struct Tag(string slug, string title, string hash_tag);

static class Errors {
    /// <summary>
    /// "categories" must contain the value: `blog`
    /// </summary>
    public const string CA0001 = "\"categories\" must contain the value: blog (" + nameof(CA0001) + ")";
    /// <summary>
    /// When "categories" contains `link`, a `link` with an URL must exist in the front matter
    /// </summary>
    public const string CA0002 = "When \"categories\" contains \"link\", a \"link\" with an url must exist in the front matter (" + nameof(CA0002) + ")";
    
    /// <summary>
    /// "date" is missing
    /// </summary>
    public const string DA0001 = "\"date\" is missing{0} (" + nameof(DA0001) + ")";
    /// <summary>
    /// "date" is in the future
    /// </summary>
    public const string DA0002 = "\"date\" is in the future (" + nameof(DA0002) + ")";
    /// <summary>
    /// "last_modified_at" is in the future
    /// </summary>
    public const string DA0003 = "\"last_modified_at\" is in the future (" + nameof(DA0003) + ")";
    /// <summary>
    /// "last_modified_at" in `index.html` or `archives.html` is not the same as "last_modified_at" or "date" in the newest post
    /// </summary>
    public const string DA0004 = "This is the newest post and \"last_modified_at\" in {0} is not the same as \"last_modified_at\" or \"date\" (" + nameof(DA0004) + ")";
    
    /// <summary>
    /// "meta_description" is missing
    /// </summary>
    public const string DE0001 = "\"meta_description\" is missing (" + nameof(DE0001) + ")";
    /// <summary>
    /// "meta_description" cannot contain: `TODO`
    /// </summary>
    public const string DE0002 = "\"meta_description\" cannot contain: TODO (" + nameof(DE0002) + ")";
    /// <summary>
    /// "meta_description" must be between 25 and 160 characters of length
    /// </summary>
    public const string DE0003 = "\"meta_description\" must be between 25 and 160 characters of length (" + nameof(DE0003) + ")";
    
    /// <summary>
    /// "image" is missing
    /// </summary>
    public const string IM0001 = "\"image\" is missing (" + nameof(IM0001) + ")";
    /// <summary>
    /// "image" does not exist on disk
    /// </summary>
    public const string IM0002 = "\"image\" does not exist on disk (" + nameof(IM0002) + ")";
    
    /// <summary>
    /// "layout" must have the value: `post`
    /// </summary>
    public const string LA0001 = "\"layout\" must have the value: post (" + nameof(LA0001) + ")";
    
    /// <summary>
    /// Post must have at least one tag
    /// </summary>
    public const string TA0001 = "Post must have at least one tag (" + nameof(TA0001) + ")";
    /// <summary>
    /// Could not find the tag in available tags from the subfolder `_my_tags`
    /// </summary>
    public const string TA0002 = "Could not find tag {0} in available tags from the subfolder `_my_tags` (" + nameof(TA0002) + ")";

    /// <summary>
    /// "title" is missing
    /// </summary>
    public const string TI0001 = "\"title\" is missing (" + nameof(TI0001) + ")";
    /// <summary>
    /// "title" cannot contain: `TODO`
    /// </summary>
    public const string TI0002 = "\"title\" cannot contain: TODO (" + nameof(TI0002) + ")";

    /// <summary>
    /// Path to Jekyll site not specified
    /// </summary>
    public const string JE0001 = "Please provide the location of a Jekyll site as an argument (" + nameof(JE0001) + "). Example: dotnet script main.csx -- [path_to_root_jekyll_folder]";
    /// <summary>
    /// _posts subfolder must exist
    /// </summary>
    public const string JE0002 = $"A subfolder named `_posts` containing posts must exist at the root of the Jekyll site ({nameof(JE0002)})";
    /// <summary>
    /// _posts subfolder must contain at least one post
    /// </summary>
    public const string JE0003 = "Could not find a single post in subfolder `_posts` (" + nameof(JE0003) + ")";
    /// <summary>
    /// _my_tags subfolder must exist
    /// </summary>
    public const string JE0004 = "A subfolder named `_my_tags` containing tags must exist (" + nameof(JE0004) + ")";
}
