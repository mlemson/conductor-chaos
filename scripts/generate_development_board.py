#!/usr/bin/env python3
"""Generate a GitHub issue/PR-backed Markdown roadmap. No external dependencies.

GitHub Issues is authoritative. An issue is DONE only when closed; an individual
commit does not imply completion. PRs with Refs #N/Closes #N drive review state.
"""
from __future__ import annotations

import argparse
import json
import os
import re
from collections import Counter
from pathlib import Path
from urllib.request import Request, urlopen

PHASES = ("P0", "P1", "P2")
STATUSES = ("Te doen", "Bezig", "Review", "Geblokkeerd", "Klaar")
DESCRIPTIONS = {
    "P0": "Technisch fundament + stijltest",
    "P1": "Eerste speelbare co-opervaring",
    "P2": "Bergavontuur en chaos",
}
META = re.compile(r"<!--\s*conductor-chaos-roadmap\s+phase=(P[0-2])\s+area=([\w-]+)\s*-->", re.I)
PR_LINK = re.compile(r"\b(?:closes?|fix(?:es|ed)?|resolves?|refs?|references?)\s*:?[ \t]*#(\d+)\b", re.I)
TITLE_PHASE = re.compile(r"^\[(P[0-2])\]", re.I)


def fetch_all(repo: str, endpoint: str, token: str) -> list[dict]:
    url = f"https://api.github.com/repos/{repo}/{endpoint}"
    result: list[dict] = []
    while url:
        request = Request(url, headers={
            "Authorization": f"Bearer {token}",
            "Accept": "application/vnd.github+json",
            "X-GitHub-Api-Version": "2022-11-28",
            "User-Agent": "ConductorChaosRoadmap/1.0",
        })
        with urlopen(request, timeout=35) as response:
            result.extend(json.load(response))
            links = response.headers.get("Link", "")
        url = None
        for section in links.split(","):
            if 'rel="next"' in section:
                m = re.search(r"<([^>]+)>", section)
                if m:
                    url = m.group(1)
                break
    return result


def text(value: str) -> str:
    return (str(value or "").replace("|", "\\|").replace("\n", " ")
            .replace("\r", " ").replace("`", "'"))


def task_meta(issue: dict) -> tuple[str, str] | None:
    body = issue.get("body") or ""
    match = META.search(body)
    if match:
        return match.group(1).upper(), match.group(2)
    form_phase = re.search(r"###\s+Fase\s+(P[0-2])\b", body, re.I)
    form_area = re.search(r"###\s+Onderdeel\s+([\w-]+)", body, re.I)
    if form_phase:
        return form_phase.group(1).upper(), form_area.group(1) if form_area else "Overig"
    match = TITLE_PHASE.search(issue.get("title") or "")
    return (match.group(1).upper(), "Overig") if match else None


def linked(issue: dict, prs: list[dict]) -> list[dict]:
    number = int(issue["number"])
    return [
        pr for pr in prs if pr.get("state") == "open" and
        number in {int(n) for n in PR_LINK.findall(
            (pr.get("body") or "") + " " + (pr.get("title") or ""))}
    ]


def state(issue: dict, related: list[dict]) -> str:
    labels = {str(x.get("name", "") if isinstance(x, dict) else x).lower()
              for x in issue.get("labels", [])}
    if issue.get("state") == "closed":
        return "Klaar"
    if "status:blocked" in labels or "blocked" in labels:
        return "Geblokkeerd"
    if any(not p.get("draft", False) for p in related) or "status:review" in labels:
        return "Review"
    if related or issue.get("assignee") or issue.get("assignees") or "status:in-progress" in labels:
        return "Bezig"
    return "Te doen"


def render(issues: list[dict], prs: list[dict], repo: str) -> str:
    tasks = []
    for issue in issues:
        if issue.get("pull_request"):
            continue
        meta = task_meta(issue)
        if not meta:
            continue
        related = linked(issue, prs)
        tasks.append(dict(issue=issue, phase=meta[0], area=meta[1],
                          related=related, state=state(issue, related)))
    tasks.sort(key=lambda x: (PHASES.index(x["phase"]), int(x["issue"]["number"])))
    counts = Counter(t["state"] for t in tasks)
    done = counts["Klaar"]
    total = len(tasks)
    lines = [
        "# 🚂 Conductor Chaos — live ontwikkelbord", "",
        "> **Bron van waarheid:** [GitHub Issues](https://github.com/" + repo +
        "/issues) en gekoppelde pull requests. Dit is een automatisch gegenereerde "
        "momentopname. Wijzig taken in Issues, niet rechtstreeks in dit bestand.", "",
        f"**Voortgang: {done}/{total} afgerond ({round(done * 100 / total) if total else 0}%)**", "",
        "| 📝 Te doen | 🔧 Bezig | 👀 Review | ⛔ Geblokkeerd | ✅ Klaar |",
        "| :---: | :---: | :---: | :---: | :---: |",
        "| " + " | ".join(str(counts[s]) for s in STATUSES) + " |", "",
        "## 🗺️ Mijlpalen", "", "```mermaid", "flowchart LR",
    ]
    for phase in PHASES:
        selected = [t for t in tasks if t["phase"] == phase]
        completed = sum(t["state"] == "Klaar" for t in selected)
        lines.append(f'    {phase}["{phase} · {DESCRIPTIONS[phase]} · {completed}/{len(selected)} klaar"]')
    lines.extend(["    P0 --> P1 --> P2", "```", "",
                  "| Fase | Voortgang | Inhoud |",
                  "| :-- | :-- | :-- |"])
    for phase in PHASES:
        selected = [t for t in tasks if t["phase"] == phase]
        completed = sum(t["state"] == "Klaar" for t in selected)
        filled = round(10 * completed / len(selected)) if selected else 0
        lines.append(f"| **{phase}** | {'🟩' * filled + '⬜' * (10-filled)} {completed}/{len(selected)} | {DESCRIPTIONS[phase]} |")
    lines.extend(["", "## 📋 Taken per status", ""])
    headers = {
        "Te doen": "📝 Te doen", "Bezig": "🔧 Bezig",
        "Review": "👀 Klaar voor review", "Geblokkeerd": "⛔ Geblokkeerd",
        "Klaar": "✅ Afgerond",
    }
    for stage in STATUSES:
        rows = [t for t in tasks if t["state"] == stage]
        lines.extend([f"### {headers[stage]} ({len(rows)})", ""])
        if not rows:
            lines.extend(["*Geen taken in deze kolom.*", ""])
            continue
        lines.extend(["| Fase | Taak | Onderdeel | Eigenaar | Pull request |",
                      "| :-- | :-- | :-- | :-- | :-- |"])
        for t in rows:
            issue = t["issue"]
            owners = [a.get("login", "") for a in issue.get("assignees", [])] or (
                [issue["assignee"].get("login", "")] if issue.get("assignee") else [])
            ownership = ", ".join("@" + o for o in owners if o) or "—"
            linked_prs = ", ".join(
                f"[#{p['number']}]({p['html_url']})" for p in t["related"]) or "—"
            title = re.sub(r"^\[P[0-2]\]\s*", "", issue.get("title", ""))
            lines.append(
                f"| {t['phase']} | [#{issue['number']} {text(title)}]({issue['html_url']}) "
                f"| {text(t['area'])} | {text(ownership)} | {linked_prs} |")
        lines.append("")
    tracked = {int(t["issue"]["number"]) for t in tasks}
    unlinked = [p for p in prs if p.get("state") == "open" and
                not ({int(n) for n in PR_LINK.findall(
                    (p.get("body") or "") + " " + (p.get("title") or ""))} & tracked)]
    lines.extend(["## 🔗 Open pull requests zonder gekoppelde roadmap-taak", ""])
    if unlinked:
        for p in unlinked:
            lines.append(
                f"- [#{p['number']} {text(p['title'])}]({p['html_url']}) "
                "— verwijs in de PR-omschrijving naar `Refs #nummer` of `Closes #nummer`.")
    else:
        lines.append("Alle open PR's zijn gekoppeld of er zijn geen open PR's.")
    lines.extend(["", "## 🤝 Samenwerken met mensen en agents", "",
                  "1. Kies of maak een issue en wijs een eigenaar toe zodra het werk begint.",
                  "2. Maak een branch en verwijs in de PR-omschrijving naar `Refs #nummer` "
                  "tijdens ontwikkeling, of `Closes #nummer` als de volledige taak is opgelost.",
                  "3. Draft PR = **Bezig**; review-ready PR = **Review**; "
                  "gesloten issue = **Klaar**. Label `status:blocked` gaat vóór andere open statussen.",
                  "4. Het bord wordt automatisch ververst bij issue/PR-events en pushes naar `main` "
                  "zodra de workflow naar main is gemerged.", "",
                  "**Let op:** één commit of geslaagde build betekent niet dat een hele taak is afgerond.",
                  "", "Zie [samenwerkingsregels](DEVELOPMENT_WORKFLOW.md) en "
                  "[technische roadmap](ROADMAP.md).", ""])
    return "\n".join(lines)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--issues-json", type=Path)
    parser.add_argument("--prs-json", type=Path)
    parser.add_argument("--output", type=Path, default=Path("Docs/PROJECT_BOARD.md"))
    args = parser.parse_args()
    if (args.issues_json is None) != (args.prs_json is None):
        parser.error("Supply both fixture JSON files, or neither.")
    repository = os.getenv("GITHUB_REPOSITORY", "mlemson/conductor-chaos")
    if args.issues_json:
        issues = json.loads(args.issues_json.read_text(encoding="utf-8"))
        prs = json.loads(args.prs_json.read_text(encoding="utf-8"))
    else:
        token = os.getenv("GITHUB_TOKEN")
        if not token:
            parser.error("GITHUB_TOKEN required (or supply JSON fixtures).")
        issues = fetch_all(repository, "issues?state=all&per_page=100", token)
        prs = fetch_all(repository, "pulls?state=all&per_page=100", token)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    content = render(issues, prs, repository)
    args.output.write_text(content, encoding="utf-8")
    print(f"Wrote {args.output} — {len(content)} bytes")


if __name__ == "__main__":
    main()
