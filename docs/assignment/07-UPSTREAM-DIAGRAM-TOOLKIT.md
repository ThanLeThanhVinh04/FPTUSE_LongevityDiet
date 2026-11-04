# Diagram Toolkit — Source Sync & Curated Skills

Source snapshot: **2026-11-04**. All **21 distinct** repositories requested were shallow-cloned successfully, without package install or executing third-party code:

- Parent folder: `D:\PRN232\_diagram_toolkit_sources`
- Source synchronization script: `sync-diagram-sources.ps1`
- Source audit trail with every exact commit: `sync-report.tsv`
- Curated skill installation script: `install-curated-skills.ps1`

## What was actually installed into project agents

| Project-local skill | Source (latest cloned snapshot) | Intended role |
| --- | --- | --- |
| `.agents/skills/upstream-drawio-skill` | Agents365-ai/drawio-skill | Editable diagrams.net XML; validation/export guidance |
| `.agents/skills/upstream-diagram-design` | cathrynlavery/diagram-design | Layout, readability, diagram semantics |
| `.agents/skills/upstream-drawio-ai-kit` | sparklabx/drawio-ai-kit | Draw.io layout workflow; requires drawio-ai CLI if executed |
| `.agents/skills/upstream-scientific-drawio` | icebird1998/drawio-scientific-illustrator | Scientific illustration conversion to editable draw.io |

Files are copied with upstream license notices when present. Existing project-local diagram/C4/QA skills were **preserved** rather than overwritten.

## All remaining source repositories and their role

| Repository | Kind / integration status |
| --- | --- |
| jgraph/drawio-desktop | Desktop app source; not built/installed |
| DayuanJiang/next-ai-draw-io | AI diagram editor source; not installed |
| hediet/vscode-drawio | VS Code extension source; not installed into VS Code |
| jgraph/drawio | Core diagrams.net editor source |
| lgazo/drawio-mcp-server | MCP server source; not registered/started |
| jgraph/drawio-libs | Library/source |
| jgraph/drawio-diagrams | Reference diagrams/examples |
| algenty/grafana-flowcharting | Grafana integration source |
| ljacobsson/cfn-diagram | CloudFormation diagram source |
| icebird1998/scientific-illustrator | Wider scientific visualization plugin source |
| tobiashochguertel/c4-draw.io | C4 draw.io stencils/templates; link appeared twice in the request |
| excalidraw/excalidraw | Alternative drawing web app source |
| tt-a1i/archify | Code architecture analysis source |
| mermaid-js/mermaid | Diagram-as-code engine source |
| tldraw/tldraw | Alternative canvas source |
| mingrammer/diagrams | Python architecture-as-code source |
| drawdb-io/drawdb | Interactive SQL relationship designer source |

The four installed skill repositories are also present under the source folder. Thus **all 21 unique requested sources** were cloned; only four explicit task-relevant agent skills were copied into the project. Arbitrarily copying all apps into `.agents/skills` would break separation of concerns and does not install MCP servers or GUI editors.

## Security and reproducibility

- Git shallow clone includes the checked-out latest upstream source + commit hash, **not full Git history**.
- Updating sources uses `git fetch` plus `git pull --ff-only`, preserving conflicting local changes instead of destructive reset.
- Do not run downloaded scripts, `npm install`, containers, MCP servers or browser extensions before reviewing their licenses, package dependencies and security boundaries.
- Current project diagram deliverables do **not depend on** uninstalled third-party runtimes.
- Portable draw.io Desktop v32.6.1 was downloaded from the official release and SHA-256 verified, under `D:\\PRN232\\_diagram_toolkit_sources\\drawio-portable-32.6.1\\draw.io.exe`. It exported 17 PNG preview pages and 2 PDFs. Portable `uv` v0.13.7 was SHA-256 verified to run existing Python previews without changing global PATH.

## Current artifacts

- `05-c2-component-architecture.drawio` — eight-page component architecture.
- `06-all-database-erd.drawio` — nine-page database documentation.
- `scripts/Validate-C2DbDiagrams.ps1` — structural XML checks.
- `cloudflare/refresh-sessions` — proposed Cloudflare D1 service source, **not deployed**.

Use diagrams.net (File → Open From → Device) or a verified installation of draw.io Desktop to edit the files.
