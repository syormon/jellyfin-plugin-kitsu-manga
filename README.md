<h1 align="center">Jellyfin Kitsu Manga Plugin</h1>
<h3 align="center">A manga metadata provider for <a href="https://jellyfin.org">Jellyfin</a> Books libraries</h3>

<p align="center">
<a href="https://github.com/syormon/jellyfin-plugin-kitsu-manga">
<img alt="GPLv2 License" src="https://img.shields.io/github/license/syormon/jellyfin-plugin-kitsu-manga.svg"/>
</a>
<a href="https://github.com/syormon/jellyfin-plugin-kitsu-manga/releases">
<img alt="Current Release" src="https://img.shields.io/github/release/syormon/jellyfin-plugin-kitsu-manga.svg"/>
</a>
</p>

## About
This plugin adds a manga metadata provider for [Kitsu](https://kitsu.app). It is based on the
[Jellyfin Kitsu plugin](https://github.com/jellyfin/jellyfin-plugin-kitsu), which provides anime metadata.

It only applies to libraries with the **Books** content type.

### How manga are matched
A folder in a Books library that is named after a manga *is* that manga. The folder gets the title, overview, genres,
tags, rating, age rating, magazine, authors, poster and backdrop. The books inside it are its volumes / chapters: they keep
their own name and cover, and are only linked to the manga (series name, genres, tags and age rating).

```
Manga/One Piece/One Piece v01.cbz
Manga/One Piece/Vol. 02.cbz
Manga/Berserk/Volume 01/Chapter 001.cbz
```

A book that is not inside a manga folder, for example a one-shot at the root of the library, is matched by its own name
and gets the full metadata and artwork itself. Volume and chapter markers (`v01`, `Vol. 3`, `Chapter 12`, `- 004`,
`(2019)`, `[Digital]`, ...) are ignored when matching.

### Volume and chapter numbers
The volume and chapter number of a book are read from the name of its file (`v01`, `Vol. 3`, `c012`, `Chapter 12.5`, `#4`,
`第1巻`, `第3話`, or a bare number such as `One Piece 003`, which is taken as the volume). Chapters can also be grouped in a
folder per volume (`One Piece/Volume 01/Chapter 001.cbz`). The numbers are stored as the index number of the book
(chapter, with the volume as parent index number, or the volume when there is no chapter) and in its sort title,
so the books of a manga sort by volume, then chapter.

A manga is only picked automatically when one of its titles matches the name exactly. Jellyfin has no *Identify* for folders,
so to match a folder by hand, edit its metadata, paste the url of the manga on Kitsu (for example `https://kitsu.app/manga/one-piece`)
into the **Kitsu Manga** id and refresh the folder. The slug (`one-piece`) or the numeric id work as well.

### Languages
Files and folders can be named in any language Kitsu has a title for, so Japanese names work as well as English and romaji ones:

```
Manga/進撃の巨人/進撃の巨人 第01巻.cbz
Manga/鬼滅の刃/第３巻.cbz
```

By default titles are returned in the preferred metadata language of the library (for example Japanese for a library set to
Japanese), falling back to English. A fixed title language (English, Japanese, romaji or canonical) can be picked in the plugin settings.

Kitsu only has titles in multiple languages. Overviews, genres and tags are always in English.

## Installation

[See the official documentation for install instructions](https://jellyfin.org/docs/general/server/plugins/index.html#installing).

After installing, enable **Kitsu Manga** as a metadata downloader and image fetcher for Books in the settings of your Books library.
Manga folders are handled automatically.

## Build

1. To build this plugin you will need [.NET 10](https://dotnet.microsoft.com/download/dotnet/10.0).

2. Build plugin with following command
  ```
  dotnet publish --configuration Release --output bin
  ```

3. Place the dll-file in the `plugins/kitsu-manga` folder (you might need to create the folders) of your JF install

## Releasing

To release the plugin we recommend [JPRM](https://github.com/oddstr13/jellyfin-plugin-repository-manager) that will build and package the plugin.
For additional context and for how to add the packaged plugin zip to a plugin manifest see the [JPRM documentation](https://github.com/oddstr13/jellyfin-plugin-repository-manager) for more info.

## Licence

This plugins code and packages are distributed under the GPLv2 License. See [LICENSE](./LICENSE) for more information.
