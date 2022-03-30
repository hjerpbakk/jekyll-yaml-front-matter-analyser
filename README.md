# Jekyll YAML front matter validator

[![Docker Pulls](https://img.shields.io/docker/pulls/hjerpbakk/jekyll-front-matter-analyser.svg?style=popout)](https://hub.docker.com/r/hjerpbakk/jekyll-front-matter-analyser)

[My blog](https://hjerpbakk.com) is built using the excellent static site generator [Jekyll][1]. I write my posts in [Markdown][2] with a [YAML][3] front matter block that tells Jekyll these files should be processed according to the values specified. It works great but has one drawback.

What if you specify invalid values? Jekyll doesn't care so long as the values are of the type expected by the variable. The result might not be would you'd like, but Jekyll will bravely try to build most of what you throw at it and you'll need to visually inspect the site or the source to find errors. As a software engineer, this is no good. I need to fail fast, and if compilation does succeed, the resulting artifacts need to be verified by tests.

Enter this project.

## Example

Given the following front matter:

```yaml
---
categories:
- blog
layout: post
title: Jekyll YAML front matter validator
meta_description:
image: /img/
date: 2019-07-08T12:00:00.0000000+00:00
tags:
- jekyll
- dotnet
- dotnet-script
- docker
---
```

My validator will report the following errors:

<img width="682" alt="Validation result" src="https://hjerpbakk.com/img/jekyll-yaml-front-matter-validator/jekyll-yaml-front-matter-validator.png">

- `meta_description` is empty.
- `image` contains an incomplete path that will not exist on the published site.
- For this example, the `date` was also set in the future and Jekyll would not have generated the post to be published.

## Usage

### Running through dotnet-script

If [dotnet-script][6] is installed on your local machine, download the script and run it thusly:

```bash
dotnet script main.csx -- [path_to_root_jekyll_folder]
```

Where `[path_to_root_jekyll_folder]` is the path to the root Jekyll folder. The script will look for posts in the `_posts` subfolder.

### Running using Docker

Using Docker, navigate to your root Jekyll folder and run this command in the Terminal:

```bash
docker run --rm -it --volume="$PWD:/scripts:ro" hjerpbakk/jekyll-front-matter-analyser
```

The image can be found on [Docker Hub][7].

### Continuous integration

I run the validator as part of [my blog's](https://hjerpbakk.com) CI pipeline using [CircleCI][8]. CircleCI is configured using a `.circleci/config.yml`:

```yaml
version: 2
jobs:
  build:
    machine: true
    steps:
      - checkout
      - run: chmod +x ./test.sh
      - run: ./test.sh
```

Where the `test.sh` Bash script contains:

```bash
#!/usr/bin/env bash
set -e

docker pull hjerpbakk/jekyll-front-matter-analyser
docker run --rm -it --volume="$PWD:/scripts:ro" hjerpbakk/jekyll-front-matter-analyser
```

## Configuration

You can configure the validator by either whitelisting specific files or ignore specific rules for a single file.

### Whitelist files

1. Add an empty text file named `.frontmatterignore` to your root Jekyll folder.
2. Add filenames to be ignored during analysis separated by newlines.

The following `.frontmatterignore` will ignore the posts `2014-1-16-os-x-script-for-fetching-app-store-icons.html` and `2018-06-29-beautiful-code-fira-code.md` during analysis:

```text
2014-1-16-os-x-script-for-fetching-app-store-icons.html
2018-06-29-beautiful-code-fira-code.md
```

### Ignore specific rules for a single file

To ignore specific rules for a given post, create an `ignore` list in the front matter of the post:

```yaml
---
ignore:
- IM0001
- TA0001
---
```

Where `IM0001` and `TA0001` are validation errors. See below for a complete list.

## Validation rules

The available rules are:

### Categories

- **CA0001** "categories" must contain the value: `blog`
- **CA0002** When "categories" contains `link`, a `link` with an URL must exist in the front matter

### Date

- **DA0001** "date" is missing
- **DA0002** "date" is in the future
- **DA0003** "last_modified_at" is in the future
- **DA0004** "last_modified_at" in `index.html` or `archives.html` is not the same as "last_modified_at" or "date" in the newest post

### Description

- **DE0001** "meta_description" is missing
- **DE0002** "meta_description" cannot contain: `TODO`
- **DE0003** "meta_description" must be between 25 and 160 characters of length

### Image

- **IM0001** "image" is missing
- **IM0002** "image" does not exist on disk

### Layout

- **LA0001** "layout" must have the value: `post`

### Tags

- **TA0001** Post must have at least one tag
- **TA0002** Could not find the tag in the subfolder `_my_tags`

### Title

- **TI0001** "title" is missing
- **TI0002** "title" cannot contain: `TODO`

[1]: https://jekyllrb.com "Jekyll"
[2]: https://github.com/adam-p/markdown-here/wiki/Markdown-Cheatsheet "Markdown"
[3]: https://yaml.org "YAML"
[4]: https://github.com/Sankra/jekyll-yaml-front-matter-analyser "Jekyll YAML front matter validator source"
[5]: https://github.com/Sankra/jekyll-yaml-front-matter-analyser "Jekyll YAML front matter validator"
[6]: https://github.com/filipw/dotnet-script "dotnet-script"
[7]: https://hub.docker.com/r/hjerpbakk/jekyll-front-matter-analyser "Docker Hub"
[8]: https://circleci.com/ "CircleCI"
