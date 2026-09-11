---
description: Tag the current commit as the next release and push the tag
allowed-tools: Bash(git tag:*), Bash(git push origin:*), Bash(git status:*), Bash(git log:*), Bash(git rev-list:*), Bash(git fetch:*), Bash(git cat-file:*)
---

## Context

- Latest release tag: !`git tag --list 'v[0-9]*.[0-9]*' --sort=-v:refname | head -1`
- Working tree: !`git status --short`
- Branch: !`git status --short --branch | head -1`
- Commit to tag: !`git log --oneline -1`

## Task

Tag the current commit as the next release and push that tag to `origin`.

1. Take the latest release tag above and increment its minor version:
   `v<major>.<minor>` becomes `v<major>.<minor + 1>` (so `v1.207` becomes `v1.208`).
   Leave the major version alone. If the repository has no release tag yet, use `v1.1`.
2. Stop and tell the user instead of tagging when:
   - the working tree isn't clean, or
   - the branch is ahead of its remote — the commits have to be pushed first, or
   - a tag with the new version already exists.
3. Create the annotated tag on `HEAD`, with the version as its message — that is how
   the existing tags are written: `git tag -a v1.208 -m v1.208`
4. Push it: `git push origin v1.208`
5. Report the tag you created and the commit it points at.

Don't commit anything, don't push the branch, and don't create the tag anywhere but on `HEAD`.
