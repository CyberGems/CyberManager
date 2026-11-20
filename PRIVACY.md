# Privacy — CyberManager

CyberManager runs on your PC and keeps your data on your PC. It has **no account, no telemetry, no analytics SDK and no ads**. It never sends your startup approvals, monitored processes or settings to its author or to anyone else.

Everything it knows (startup approvals, monitored processes or settings) stays in `%LOCALAPPDATA%\CyberManager\settings.json` and a startup-approvals log. Older builds used `%ProgramData%\CyberManager\settings.json`. The uninstaller removes its scheduled task; app data is kept.

## When CyberManager goes online

These are all the connections CyberManager makes by itself. None of them sends your startup approvals, monitored processes or settings. Like any request on the internet, the server sees your IP address.

| When | Where | What for | How to turn it off |
|---|---|---|---|
| On startup | `api.github.com/repos/CyberGems/CyberManager/releases/latest` | Check for a newer version | `AutoCheckForUpdates` |

A link you click inside the app, or a web page you open in your browser, is your own action and is not a connection the app makes by itself.

## What CyberManager reads on your PC

CyberManager reads running processes, system metrics, startup entries and disk usage. That information stays on your screen. Anything that changes the system asks for confirmation first and, where possible, is backed up so it can be undone.

## Questions

Open an issue: https://github.com/CyberGems/CyberManager/issues

For a privacy-specific question, use the [contact page](https://cybergems.org/contact). The suite-wide statement is at [cybergems.org/privacy](https://cybergems.org/privacy).
