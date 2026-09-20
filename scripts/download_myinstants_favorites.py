#!/usr/bin/env python3
"""Download every favorite sound of a MyInstants user.

Why this doesn't just call myinstants-api.vercel.app/favorites:
that endpoint (abdipr/myinstants-api) only parses page 1 of the profile, so it
silently caps at 90 sounds and ignores any `page` parameter. This script uses
the same extraction the API does (the mp3 path inside each entry's
`play('...')` onclick), but walks every profile page, so no per-sound details
page fetch is needed.

MyInstants sits behind Cloudflare, which 403s curl's default User-Agent; a
browser User-Agent plus a Referer is enough, so no yt-dlp is needed either.

Sounds go straight into the Soundboard mod's sounds folder in the game's
per-user data (%USERPROFILE%\\AppData\\LocalLow\\Videocult\\Rain World\\Soundboard\\sounds),
so they show up in the mod's Add Sound tab after RELOAD CONFIG.

Usage:
    python download_myinstants_favorites.py                 # user dion823 -> the mod's sounds folder
    python download_myinstants_favorites.py someone -o out  # another user / folder
    python download_myinstants_favorites.py --list          # just list, download nothing

Re-running is safe: files that already exist are skipped.
"""
import argparse
import html
import os
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

BASE = "https://www.myinstants.com"
UA = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36"
)
RETRIES = 3
DEFAULT_OUT = os.path.join(
    os.path.expanduser("~"), "AppData", "LocalLow", "Videocult", "Rain World", "Soundboard", "sounds"
)

INSTANT_SPLIT = re.compile(r'<div class="instant">')
PLAY_RE = re.compile(r"play\('([^']+)'")
LINK_RE = re.compile(r'<a href="(/en/instant/([^"/]+)/)"[^>]*class="instant-link[^"]*"[^>]*>(.*?)</a>', re.S)
RESERVED = {"CON", "PRN", "AUX", "NUL", *(f"COM{i}" for i in range(1, 10)), *(f"LPT{i}" for i in range(1, 10))}


def fetch(url, referer=None):
    """GET url and return (content_type, body); raises urllib.error.URLError/HTTPError."""
    headers = {"User-Agent": UA}
    if referer:
        headers["Referer"] = referer
    req = urllib.request.Request(url, headers=headers)
    with urllib.request.urlopen(req, timeout=30) as resp:
        return resp.headers.get("Content-Type", ""), resp.read()


def fetch_retry(url, referer=None):
    for attempt in range(1, RETRIES + 1):
        try:
            return fetch(url, referer)
        except urllib.error.HTTPError as e:
            if e.code == 404 or attempt == RETRIES:
                raise
        except urllib.error.URLError:
            if attempt == RETRIES:
                raise
        time.sleep(2 * attempt)


def parse_sounds(page_html):
    sounds = []
    for chunk in INSTANT_SPLIT.split(page_html)[1:]:
        play = PLAY_RE.search(chunk)
        link = LINK_RE.search(chunk)
        if not play or not link:
            continue
        path, slug, title = link.groups()
        sounds.append({
            "id": slug,
            "title": html.unescape(re.sub(r"<[^>]+>", "", title)).strip(),
            "url": BASE + path,
            "mp3": urllib.parse.urljoin(BASE, play.group(1)),
        })
    return sounds


def fetch_favorites(username):
    """Walk /profile/<user>/?page=N until the site 404s or stops yielding new sounds."""
    seen, sounds = set(), []
    page = 1
    while True:
        url = f"{BASE}/en/profile/{urllib.parse.quote(username)}/?page={page}"
        try:
            _, body = fetch_retry(url)
        except urllib.error.HTTPError as e:
            if e.code == 404 and page > 1:
                break  # ran off the end
            raise
        fresh = [s for s in parse_sounds(body.decode("utf-8", "replace")) if s["id"] not in seen]
        if not fresh:
            break
        seen.update(s["id"] for s in fresh)
        sounds.extend(fresh)
        print(f"  page {page}: {len(fresh)} sounds ({len(sounds)} total)")
        page += 1
    return sounds


def safe_stem(title, fallback):
    stem = re.sub(r'[<>:"/\\|?*\x00-\x1f]', "_", title)
    stem = re.sub(r"\s+", " ", stem).strip(" .")[:100].strip(" .")
    if not stem or stem.upper() in RESERVED:
        stem = fallback
    return stem


def assign_filenames(sounds):
    """Title-based names; any names that collide (case-insensitively, as on Windows)
    all get the sound's id appended, so the result is stable across re-runs."""
    for s in sounds:
        s["_stem"] = safe_stem(s["title"], s["id"])
        ext = os.path.splitext(urllib.parse.urlparse(s["mp3"]).path)[1] or ".mp3"
        s["_ext"] = ext
    counts = {}
    for s in sounds:
        key = s["_stem"].lower()
        counts[key] = counts.get(key, 0) + 1
    for s in sounds:
        stem = s.pop("_stem")
        ext = s.pop("_ext")
        if counts[stem.lower()] > 1:
            stem = f"{stem} [{s['id']}]"
        s["file"] = stem + ext


def download(sound, out_dir):
    dest = os.path.join(out_dir, sound["file"])
    if os.path.exists(dest) and os.path.getsize(dest) > 0:
        return "skipped"
    ctype, body = fetch_retry(sound["mp3"], referer=sound["url"])
    if not body or "html" in ctype.lower():
        raise ValueError(f"unexpected response ({ctype or 'no content-type'}, {len(body)} bytes)")
    tmp = dest + ".part"
    with open(tmp, "wb") as f:
        f.write(body)
    os.replace(tmp, dest)
    return "downloaded"


def main():
    ap = argparse.ArgumentParser(description="Download all favorite sounds of a MyInstants user.")
    ap.add_argument("username", nargs="?", default="dion823")
    ap.add_argument("-o", "--out", default=DEFAULT_OUT, help="output folder (default: %(default)s)")
    ap.add_argument("--delay", type=float, default=0.3, help="seconds between downloads (default: %(default)s)")
    ap.add_argument("--list", action="store_true", help="list the favorites and exit without downloading")
    args = ap.parse_args()

    print(f"Fetching favorites of {args.username}...")
    sounds = fetch_favorites(args.username)
    if not sounds:
        sys.exit("No favorites found (wrong username, or the profile is empty).")
    assign_filenames(sounds)

    if args.list:
        for s in sounds:
            print(f"{s['file']}\t{s['mp3']}")
        print(f"{len(sounds)} favorites.")
        return

    os.makedirs(args.out, exist_ok=True)

    counts = {"downloaded": 0, "skipped": 0}
    failed = []
    for i, s in enumerate(sounds, 1):
        try:
            result = download(s, args.out)
        except (urllib.error.URLError, ValueError, OSError) as e:
            failed.append((s, e))
            print(f"[{i}/{len(sounds)}] FAILED  {s['file']}: {e}")
            continue
        counts[result] += 1
        print(f"[{i}/{len(sounds)}] {result:<10} {s['file']}")
        if result == "downloaded":
            time.sleep(args.delay)

    print(f"\nDone: {counts['downloaded']} downloaded, {counts['skipped']} already present, "
          f"{len(failed)} failed -> {os.path.abspath(args.out)}")
    for s, e in failed:
        print(f"  failed: {s['title']} <{s['url']}>: {e}")
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
