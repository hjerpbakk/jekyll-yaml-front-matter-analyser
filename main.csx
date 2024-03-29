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
        var frontMatter = Parse<FrontMatter>(post);
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

(string postFilename, AppFrontMatter frontMatter, DateTime lastModified) newestApp = (null, null, DateTime.MinValue);
var appPath = Path.GetFullPath(Path.Combine(Args[0], "_apps"));
if (Directory.Exists(appPath)) {
    var apps = "*.md;*.html".Split(';').SelectMany(g => Directory.GetFiles(appPath, g)).ToArray();
    var appMetadata = new List<(string title, string slug)>(apps.Length);
    foreach (var app in apps) {
        var appFileName = Path.GetFileName(app);
        if (whitelistedFiles.Contains(appFileName)) {
            continue;
        }

        try {
            var frontMatter = Parse<AppFrontMatter>(app);
            if (frontMatter == null) {
                verificationResults.Add(appFileName, new List<string>() { string.Format(Errors.AP0001, "") });
                continue;
            }

            if (frontMatter.last_modified_at.HasValue) {
                var lastModified = frontMatter.last_modified_at.Value;
                if (lastModified > newestApp.lastModified) {
                    newestApp = (appFileName, frontMatter, lastModified);
                    if (newestApp.frontMatter == null) {
                        WriteError(string.Format(Errors.AP0001, " from the newest app"));
                        WriteErrorSummary(1);
                        return 1;
                    }
                }
            }

            appMetadata.Add((frontMatter.title, frontMatter.slug));            
            verificationResults.Add(appFileName, frontMatter.Verify(Args[0]));
        } catch (Exception exception) {
            verificationResults.Add(appFileName, new List<string>() { exception.Message });  
        }
    }

    var privacyPath = Path.GetFullPath(Path.Combine(Args[0], "_privacy"));
    if (Directory.Exists(privacyPath)) {
        var privacyPolicies = "*.md;*.html".Split(';').SelectMany(g => Directory.GetFiles(privacyPath, g)).ToArray();
        foreach (var privacyPolicy in privacyPolicies) {
            var privacyPolicyFileName = Path.GetFileName(privacyPolicy);
            if (whitelistedFiles.Contains(privacyPolicyFileName)) {
                continue;
            }

            try {
                var frontMatter = Parse<PrivacyFrontMatter>(privacyPolicy);
                if (frontMatter == null) {
                    verificationResults.Add(privacyPolicyFileName, new List<string>() { string.Format(Errors.PR0001, "") });
                    continue;
                }
                
                verificationResults.Add(privacyPolicyFileName, frontMatter.Verify(Args[0], appMetadata));
            } catch (Exception exception) {
                verificationResults.Add(privacyPolicyFileName, new List<string>() { exception.Message });  
            }
        }
    }
}

if (newestApp.frontMatter != null) {
    var lastModifiedErrors = newestApp.frontMatter.Verify(newestApp.lastModified, newestPost.lastModified, Args[0]);
    if (verificationResults.ContainsKey(newestApp.postFilename)) {
        verificationResults[newestApp.postFilename].AddRange(lastModifiedErrors);
    } else {
        verificationResults.Add(newestApp.postFilename, lastModifiedErrors);
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

void WriteError(string error) {
    var defaultColor = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine(error);
    Console.ForegroundColor = defaultColor;
}

void WriteErrorSummary(int numberOfErrors) {
    Console.WriteLine($"{Environment.NewLine}Found {numberOfErrors} errors 🤨");
}

static T Parse<T>(string path) {
    var frontMatterText = GetFrontMatterFromPost();
    var deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();
    var frontMatter = deserializer.Deserialize<T>(frontMatterText);
    return frontMatter;

    string GetFrontMatterFromPost() {
        var fullText = File.ReadAllText(path);
        var indexOfFirstLineBreak = fullText.IndexOf('\n');
        var indexOfFrontMatterEnd = fullText.IndexOf("---\n", indexOfFirstLineBreak, StringComparison.InvariantCulture);
        if (indexOfFrontMatterEnd == -1) {
            indexOfFrontMatterEnd = fullText.IndexOf("---", indexOfFirstLineBreak, StringComparison.InvariantCulture);
        }

        var frontMatterEnd = indexOfFrontMatterEnd + 3;
        var frontMatterText = fullText.Substring(0, frontMatterEnd).Trim('-');
        return frontMatterText;
    }
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

    public List<string> Verify(DateTime lastModified, string rootPath) {
        var errors = new List<string>();
        var files = new [] { "index.html", "archives.html" };
        foreach (var file in files) {
            var filePath = Path.Combine(rootPath, file);
            if (!File.Exists(filePath)) {
                continue;
            }

            if (ignore.Contains(nameof(Errors.DA0004))) {
                continue;
            }

            var frontMatter = Parse<FrontMatter>(filePath);
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
        } else if (title.Length < 30) {
            errors.Add(Errors.TI0003);
        } else if (title.Length > 60) {
            errors.Add(Errors.TI0004);
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

record struct Tag(string slug, string title, string hash_tag);

sealed record AppFrontMatter {
    public string layout { get; init; }
    public string title { get; init; }
    public string tagline { get; init; }
    public string slug { get; init; }
    public int? ordering { get; init; }
    public string meta_description { get; init; }
    public string lang { get; init; }
    public string platform { get; init; }
    public string app_category { get; init; }
    public string image { get; init; }
    public string screenshot { get; init; }
    public int? width { get; init; }
    public int? height { get; init; }
    public string app_id { get; init; }
    public string description1 { get; init; }
    public string description2 { get; init; }
    public List<string> features { get; init; }
    public DateTime? last_modified_at { get; init; }
    public List<string> ignore { get; init; } = new List<string>();
    
    public List<string> Verify(DateTime lastModified, DateTime postLastModified, string rootPath) {
        var errors = new List<string>();
        var files = new [] { "apps.html", "archives.html" };
        foreach (var file in files) {
            var filePath = Path.Combine(rootPath, file);
            if (!File.Exists(filePath)) {
                continue;
            }

            if (ignore.Contains(nameof(Errors.AP0022))) {
                continue;
            }

            var frontMatter = Parse<FrontMatter>(filePath);
            if (frontMatter.last_modified_at != lastModified) {
                if (file == "archives.html" && lastModified < postLastModified) {
                    continue;
                }

                errors.Add(string.Format(Errors.AP0022, file));
            }
        }

        return errors;
    }

    public List<string> Verify(string rootPath) {
        var errors = new List<string>();

        // layout
        if (string.IsNullOrEmpty(layout) || layout != "app") {
            errors.Add(Errors.AP0003);
        }

        // last_modified_at
        var now = DateTime.Now;
        if (last_modified_at == null || last_modified_at == DateTime.MinValue) {
            errors.Add(Errors.AP0001);
        } else if (last_modified_at > now) {
            errors.Add(Errors.AP0002);
        }

        // title
        if (string.IsNullOrEmpty(title)) {
            errors.Add(Errors.AP0004);
        } else if (title.Contains("TODO", StringComparison.InvariantCultureIgnoreCase)) {
            errors.Add(Errors.AP0023);
        }

        // tagline
        if (string.IsNullOrEmpty(tagline)) {
            errors.Add(Errors.AP0005);
        } else if (tagline.Contains("TODO", StringComparison.InvariantCultureIgnoreCase)) {
            errors.Add(Errors.AP0023);
        }

        // slug
        if (string.IsNullOrEmpty(slug)) {
            errors.Add(Errors.AP0006);
        }

        // ordering
        if (ordering == null) {
            errors.Add(Errors.AP0007);
        }

        // meta_description
        if (string.IsNullOrEmpty(meta_description)) {
            errors.Add(Errors.AP0008);
        } else if (meta_description.Contains("TODO", StringComparison.InvariantCultureIgnoreCase)) {
            errors.Add(Errors.AP0023);
        } else if (meta_description.Length < 25 || meta_description.Length > 160) {
            errors.Add(Errors.AP0024);
        }

        // lang
        if (string.IsNullOrEmpty(lang)) {
            errors.Add(Errors.AP0009);
        }

        // platform
        if (string.IsNullOrEmpty(platform)) {
            errors.Add(Errors.AP0010);
        }

        // app_category
        if (string.IsNullOrEmpty(app_category)) {
            errors.Add(Errors.AP0011);
        }

        // image
        if (string.IsNullOrEmpty(image)) {
            errors.Add(Errors.AP0012);
        } else {
            var imagePathPart = image.Remove(0, 1);
            var imagePath = Path.GetFullPath(Path.Combine(rootPath, imagePathPart));
            if (!File.Exists(imagePath)) {
                errors.Add(Errors.AP0020);
            }
        }

        // screenshot
        if (string.IsNullOrEmpty(screenshot)) {
            errors.Add(Errors.AP0013);
        } else {
            var imagePathPart = screenshot.Remove(0, 1);
            var imagePath = Path.GetFullPath(Path.Combine(rootPath, imagePathPart));
            if (!File.Exists(imagePath)) {
                errors.Add(Errors.AP0021);
            }
        }

        // width
        if (width == null) {
            errors.Add(Errors.AP0014);
        }

        // height
        if (height == null) {
            errors.Add(Errors.AP0015);
        }

        // app_id
        if (string.IsNullOrEmpty(app_id)) {
            errors.Add(Errors.AP0016);
        }

        // description1
        if (string.IsNullOrEmpty(description1)) {
            errors.Add(Errors.AP0017);
        } else if (description1.Contains("TODO", StringComparison.InvariantCultureIgnoreCase)) {
            errors.Add(Errors.AP0023);
        }

        // description2
        if (string.IsNullOrEmpty(description2)) {
            errors.Add(Errors.AP0018);
        } else if (description2.Contains("TODO", StringComparison.InvariantCultureIgnoreCase)) {
            errors.Add(Errors.AP0023);
        }

        // features
        if (features == null || features.Count == 0) {
            errors.Add(Errors.AP0019);
        } else {
            foreach (var feature in features) {
                if (feature.Contains("TODO", StringComparison.InvariantCultureIgnoreCase)) {
                    errors.Add(Errors.AP0023);
                }
            }
        }

        // Ignored rules
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

sealed record PrivacyFrontMatter {
    public string layout { get; init; }
    public string slug { get; init; }
    public string app_title { get; init; }
    public string meta_description { get; init; }
    public DateTime? last_modified_at { get; init; }

    public List<string> Verify(string rootPath, IEnumerable<(string title, string slug)> apps) {
        var errors = new List<string>();

        // layout
        if (string.IsNullOrEmpty(layout) || (layout != "privacy_policy_with_ads" && layout != "privacy_policy")) {
            errors.Add(Errors.PR0003);
        }

        // last_modified_at
        var now = DateTime.Now;
        if (last_modified_at == null || last_modified_at == DateTime.MinValue) {
            errors.Add(Errors.PR0001);
        } else if (last_modified_at > now) {
            errors.Add(Errors.PR0002);
        }

        // app_title
        if (string.IsNullOrEmpty(app_title)) {
            errors.Add(Errors.PR0004);
        } else if (app_title.Contains("TODO", StringComparison.InvariantCultureIgnoreCase)) {
            errors.Add(Errors.PR0006);
        } else if (!apps.Any(app => app.title == app_title)) {
            errors.Add(Errors.PR0007);
        }

        // slug
        if (string.IsNullOrEmpty(slug)) {
            errors.Add(Errors.PR0005);
        } else if (!apps.Any(app => app.slug == slug)) {
            errors.Add(Errors.PR0008);
        }

        // meta_description
        if (string.IsNullOrEmpty(meta_description)) {
            errors.Add(Errors.PR0009);
        } else if (meta_description.Contains("TODO", StringComparison.InvariantCultureIgnoreCase)) {
            errors.Add(Errors.PR0006);
        } else if (meta_description.Length < 25 || meta_description.Length > 160) {
            errors.Add(Errors.PR0010);
        }

        return errors;
    }
}

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
    /// "title" is too short, must be between 30 and 60 characters
    /// </summary>
    public const string TI0003 = "\"title\" is too short, must be between 30 and 60 characters (" + nameof(TI0003) + ")";
    /// <summary>
    /// "title" is too long, must be between 30 and 60 characters
    /// </summary>
    public const string TI0004 = "\"title\" is too long, must be between 30 and 60 characters (" + nameof(TI0004) + ")";

    /// <summary>
    /// Path to Jekyll site not specified
    /// </summary>
    public const string JE0001 = "Please provide the location of a Jekyll site as an argument (" + nameof(JE0001) + "). Example: dotnet script main.csx -- [path_to_root_jekyll_folder]";
    /// <summary> d
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

    /// <summary>
    /// "last_modified_at" is missing
    /// </summary>
    public const string AP0001 = "\"last_modified_at\" is missing (" + nameof(AP0001) + ")";
    /// <summary>
    /// "last_modified_at" is in the future
    /// </summary>
    public const string AP0002 = "\"last_modified_at\" is in the future (" + nameof(AP0002) + ")";
    /// <summary>
    /// "layout" must have the value: `app`
    /// </summary>
    public const string AP0003 = "\"layout\" must have the value: app (" + nameof(AP0003) + ")";
    /// <summary>
    /// "title" is missing
    /// </summary>
    public const string AP0004 = "\"title\" is missing (" + nameof(AP0004) + ")";
    /// <summary>
    /// "tagline" is missing
    /// </summary>
    public const string AP0005 = "\"tagline\" is missing (" + nameof(AP0005) + ")";
    /// <summary>
    /// "slug" is missing
    /// </summary>
    public const string AP0006 = "\"slug\" is missing (" + nameof(AP0006) + ")";
    /// <summary>
    /// "ordering" is missing
    /// </summary>
    public const string AP0007 = "\"ordering\" is missing (" + nameof(AP0007) + ")";
    /// <summary>
    /// "meta_description" is missing
    /// </summary>
    public const string AP0008 = "\"meta_description\" is missing (" + nameof(AP0008) + ")";
    /// <summary>
    /// "lang" is missing
    /// </summary>
    public const string AP0009 = "\"lang\" is missing (" + nameof(AP0009) + ")";
    /// <summary>
    /// "platform" is missing
    /// </summary>
    public const string AP0010 = "\"platform\" is missing (" + nameof(AP0010) + ")";
    /// <summary>
    /// "app_category" is missing
    /// </summary>
    public const string AP0011 = "\"app_category\" is missing (" + nameof(AP0011) + ")";
    /// <summary>
    /// "image" is missing
    /// </summary>
    public const string AP0012 = "\"image\" is missing (" + nameof(AP0012) + ")";
    /// <summary>
    /// "screenshot" is missing
    /// </summary>
    public const string AP0013 = "\"screenshot\" is missing (" + nameof(AP0013) + ")";
    /// <summary>
    /// "width" is missing
    /// </summary>
    public const string AP0014 = "\"width\" is missing (" + nameof(AP0014) + ")";
    /// <summary>
    /// "height" is missing
    /// </summary>
    public const string AP0015 = "\"height\" is missing (" + nameof(AP0015) + ")";
    /// <summary>
    /// "app_id" is missing
    /// </summary>
    public const string AP0016 = "\"app_id\" is missing (" + nameof(AP0016) + ")";
    /// <summary>
    /// "description1" is missing
    /// </summary>
    public const string AP0017 = "\"description1\" is missing (" + nameof(AP0017) + ")";
    /// <summary>
    /// "description2" is missing
    /// </summary>
    public const string AP0018 = "\"description2\" is missing (" + nameof(AP0018) + ")";
    /// <summary>
    /// "features" is missing
    /// </summary>
    public const string AP0019 = "\"features\" is missing (" + nameof(AP0019) + ")";
    /// <summary>
    /// "image" does not exist on disk
    /// </summary>
    public const string AP0020 = "\"image\" does not exist on disk (" + nameof(AP0020) + ")";
    /// <summary>
    /// "screenshot" does not exist on disk
    /// </summary>
    public const string AP0021 = "\"screenshot\" does not exist on disk (" + nameof(AP0021) + ")";
    /// <summary>
    /// "last_modified_at" in `archive.html` or `apps.html` is not the same as "last_modified_at" or "date" in the newest app
    /// </summary>
    public const string AP0022 = "This is the newest app and \"last_modified_at\" in {0} is not the same as \"last_modified_at\" (" + nameof(AP0022) + ")";
    /// <summary>
    /// Front matter contains TODO
    /// </summary>
    public const string AP0023 = "Front matter contains TODO (" + nameof(AP0023) + ")";
    /// <summary>
    /// "meta_description" must be between 25 and 160 characters of length
    /// </summary>
    public const string AP0024 = "\"meta_description\" must be between 25 and 160 characters of length (" + nameof(AP0024) + ")";

    /// <summary>
    /// "last_modified_at" is missing
    /// </summary>
    public const string PR0001 = "\"last_modified_at\" is missing (" + nameof(PR0001) + ")";
    /// <summary>
    /// "last_modified_at" is in the future
    /// </summary>
    public const string PR0002 = "\"last_modified_at\" is in the future (" + nameof(PR0002) + ")";
    /// <summary>
    /// "layout" must have the value: `privacy_policy_with_ads` or `privacy_policy`
    /// </summary>
    public const string PR0003 = "\"layout\" must have the value: privacy_policy_with_ads or privacy_policy (" + nameof(PR0003) + ")";
    /// <summary>
    /// "app_title" is missing
    /// </summary>
    public const string PR0004 = "\"app_title\" is missing (" + nameof(PR0004) + ")";
    /// <summary>
    /// "slug" is missing
    /// </summary>
    public const string PR0005 = "\"slug\" is missing (" + nameof(PR0005) + ")";
    /// <summary>
    /// Front matter contains TODO
    /// </summary>
    public const string PR0006 = "Front matter contains TODO (" + nameof(PR0006) + ")";
    /// <summary>
    /// "app_title" must be found in a corresponding app
    /// </summary>
    public const string PR0007 = "\"app_title\" must be found in a corresponding app (" + nameof(PR0007) + ")";
    /// <summary>
    /// "slug" must be found in a corresponding app
    /// </summary>
    public const string PR0008 = "\"slug\" must be found in a corresponding app (" + nameof(PR0008) + ")";
    /// <summary>
    /// "meta_description" is missing
    /// </summary>
    public const string PR0009 = "\"meta_description\" is missing (" + nameof(PR0009) + ")";
    /// <summary>
    /// "meta_description" must be between 25 and 160 characters of length
    /// </summary>
    public const string PR0010 = "\"meta_description\" must be between 25 and 160 characters of length (" + nameof(PR0010) + ")";
}
