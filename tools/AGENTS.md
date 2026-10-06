# Engineering tools

## Absolute filename and folder-name prohibition

Never create or introduce a new tool file or folder whose basename contains `todir` in any casing. This is absolute; tool paths must describe their responsibility.

`tools/` contains reusable developer and engineering tools. A one-off command does not automatically justify a tool, and this directory must not become a dumping ground. Keep application business logic out of tools; define each tool's purpose, safety boundaries and verification when it is introduced.
