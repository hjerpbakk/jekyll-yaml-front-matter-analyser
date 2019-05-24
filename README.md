# jekyll-yaml-front-matter-analyser

Analyses Jekyll YAML front matter and reports errors and omissions.

## Usage

```shell
dotnet script main.csx -- [path_to_root_jekyll_folder]
```

## Configuration

### Whitelist files

1. Add an empty text file named `.frontmatterignore` to your root Jekyll folder
2. Add filenames to be ignored during analysis separated by newlines 
