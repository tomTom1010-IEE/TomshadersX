# GitHub Publication

Repository: https://github.com/tomTom1010-IEE/TomshadersX

The user approved publishing this source package, an English-default showcase
and English manual while retaining the Chinese versions on 2026-10-04. The
existing remote AGPL-3.0 license is retained, with the original MIT upstream
notice in LICENSES/MIT-Upstream.txt. Remote history is preserved.

## Site Layout

- GitHub Pages source: main branch, /docs, with .nojekyll.
- index.html and Gallery.html: English showcase.
- Gallery.zh-CN.html: Chinese showcase.
- UserManual.html / UserManual.zh-CN.html: matching language manuals.
- Numbered raw PNG files: the same 96 actual GPU captures for both languages.
- *-board.png, Overview.png and Contact-*.png: English title composites.
- *.zh-CN.png: Chinese title composites.
- report.json: capture settings and pixel-check evidence.

README embeds all 24 English comparison boards because GitHub's repository
README cannot use the standalone gallery's custom layout styles. The Pages site
retains the original responsive four/two/one-column layout and expandable
material settings. No extra landing page, analytics or external scripts are used.

## Regeneration

From the host Unity project, using PowerShell 7 on Windows:

```powershell
& Assets/Mods/TomShadersX/Tests/Export-XShowcase.ps1 -ReportDirectory CodexBridge/Reports/XShowcase-20261004-140502 -OutputDirectory Assets/Mods/TomShadersX/docs
& Assets/Mods/TomShadersX/Tests/Test-XShowcaseSite.ps1
```

This regenerates presentation assets from the existing report and raw images.
Do not rerun Unity rendering merely to translate titles. The earlier offline
Chinese delivery remains untouched. When serving under a repository subpath,
keep local links relative and filenames case-correct.

Read user manuals from Documents as source, never from generated HTML. Changes
to this publication do not imply a new shader release or new game acceptance.
