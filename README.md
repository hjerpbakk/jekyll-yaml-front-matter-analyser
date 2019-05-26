# jekyll-yaml-front-matter-analyser

Analyses Jekyll YAML front matter and reports errors and omissions.

## Usage

If dotnet script is installed on your local machine:

```shell
dotnet script main.csx -- [path_to_root_jekyll_folder]
```

Using Docker, navigate to your root Jekyll folder and run this command in the Terminal:

```shell
docker run --rm -it --volume="$PWD:/scripts:ro" hjerpbakk/jekyll-front-matter-analyser
```

## Configuration

### Whitelist files

1. Add an empty text file named `.frontmatterignore` to your root Jekyll folder
2. Add filenames to be ignored during analysis separated by newlines 

### Ignore specific rules for a given post

To ignore specific rules for a given post, create an `ignore` list in the front matter of the post:

```yml
---
ignore:
- IM0001
- TA0001
---
```

Where `IM0001` and `TA0001` are validation errors. 

#### Validation errors
The available errors are:

##### Categories
- **CA0001** "categories" must contain the value: blog
- **CA0002** When "categories" contains "link", a "link" with an url must exist in the front matter

##### Layout
- **LA0001** "layout" must have the value: post

##### Title
- **TI0001** "title" is missing
- **TI0002** "title" cannot contain: TODO

##### Description
- **DE0001** "meta_description" is missing
- **DE0002** "meta_description" cannot contain: TODO

##### Date
- **DA0001** "date" is missing
- **DA0002** "date" was in the future

##### Image
- **IM0001** "image" is missing
- **IM0002** "image" did not exist on disk

##### Tags
- **TA0001** Post must have at least one tag
- **TA0002** Could not find tag in available tags
