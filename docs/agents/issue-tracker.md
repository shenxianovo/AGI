# Issue tracker — local markdown

Issues live as markdown files under `.scratch/<feature>/`.

## Structure

Each issue is a file: `.scratch/<feature>/issue.md`

Frontmatter fields:
- `title` — short summary
- `status` — open | closed
- `labels` — list of triage labels
- `created` — ISO date

## Creating an issue

Create a new directory under `.scratch/` named after the feature or bug, then write `issue.md` with the frontmatter and body.

## Listing issues

Glob `.scratch/*/issue.md` and read frontmatter.

## Closing an issue

Set `status: closed` in the frontmatter.
