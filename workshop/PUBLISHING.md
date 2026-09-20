# Publishing Custom Soundboard to the Steam Workshop

Everything here was checked against Rain World v1.11.8's own code (`SteamWorkshopUploader`, `RainWorldSteamManager`,
`ModManager.LoadModFromJson`, `Menu.Remix.InternalOI_Stats`). The game has its own uploader built into Remix:
there is no separate tool, and **the game uploads the mod folder that is installed in the game, not this repo**.

This folder (`workshop/`) is deliberately outside `mod/`, so none of it is shipped to players.

| File | What it is |
|---|---|
| `description.bbcode.txt` | The Workshop page description, in Steam BBCode. Paste it into the item's Description box. |
| `make-thumbnail.ps1` | Regenerates the placeholder `mod/thumbnail.png`. Optional: replace the PNG with anything better. |
| `PUBLISHING.md` | This checklist. |

## What the uploader needs (and enforces)

- **Steam must be running and you must own Rain World on that account.** The upload button only appears when the game
  could talk to Steam (Steam has to be running and you should launch Rain World from Steam).
- **The mod must be a normal local mod** in `RainWorld_Data\StreamingAssets\mods\dion_soundboard` - not a copy you got by
  subscribing. (Subscribed mods can't be uploaded.)
- **`modinfo.json`** in the root of the mod folder. The uploader sends `name` (Workshop title), `description` (Workshop
  description, `<LINE>` becomes a line break), `tags`, and the `id`, `version`, `authors`, `target_game_version`,
  `requirements`, `requirements_names` fields as hidden item metadata. Fields the game understands:
  `id`, `name`, `version`, `hide_version`, `target_game_version`, `authors`, `description`, `youtube_trailer_id`,
  `requirements`, `requirements_names`, `tags`, `priorities`, `checksum_override_version`. `mod/modinfo.json` has all
  the ones that make sense for this mod.
- **`thumbnail.png`** in the root of the mod folder (optional, but you want one). The uploader **refuses** an image that is
  1,000,000 bytes or larger ("must be less than 1 MB") or whose height/width isn't 16:9 ("should have a 16:9 aspect
  ratio"). `mod/thumbnail.png` is 1280x720, about 145 KB. The same image is shown in Remix's mod list.
- **The id must be free.** Before uploading the game asks Steam for an item whose hidden `id` tag equals
  `dion_soundboard`. None found: it creates a new item. Found and it's yours: it updates it. Found and it belongs to
  somebody else: it stops with "This mod already exists on the workshop by another author." (I couldn't check this
  offline. If it happens, the id must change: `modinfo.json`, `MOD_ID` in `src/Plugin.cs` and `deploy.ps1`'s `$modId`.)
- **Size:** the game has no size check of its own beyond the thumbnail; Steam can reject with "Limit exceeded". `mod/` is
  about 62 MB, and other Rain World Workshop items here are several hundred MB up to about 1 GB, so this is fine.
- Everything in the installed mod folder is uploaded, including `plugins\SoundboardMod.pdb` and anything stray left
  behind from older installs. See step 1.

## Publish for the first time

1. **Get the installed folder exactly right.** In this repo: `git checkout main && git pull`, make sure `mod/modinfo.json`,
   `[BepInPlugin]` in `src/Plugin.cs` and the git tag agree on the version, and that `mod/plugins/SoundboardMod.dll` is the
   release build. Then, to avoid uploading stale leftovers, delete
   `...\Rain World\RainWorld_Data\StreamingAssets\mods\dion_soundboard` (your own soundboard.yaml and sounds are safe: they live in
   `%USERPROFILE%\AppData\LocalLow\Videocult\Rain World\Soundboard`, not in the mod folder) and run
   `./scripts/deploy.ps1 -SkipBuild`. Check the folder contains only `modinfo.json`, `thumbnail.png`, `soundboard.yaml`,
   `plugins\` and `sounds\`.
2. **Decide about the example sounds first** (see the sound warning in the PR). Once the mod is public, takedown
   requests for a clip are much more annoying than trimming the set now.
3. Launch Rain World **from Steam**. Open **Remix**, enable Custom Soundboard if it isn't, and click it in the list so its
   information panel is showing.
4. Press the small button next to the mod's name at the top of that panel (tooltip "Upload the selected mod to Steam
   Workshop"), then **UPLOAD** in the confirmation. The dialogs go: *Gathering information*, *Preparing for upload*,
   (first time only: *You must accept the End User License Agreement* - press CONTINUE, accept it in the Steam overlay, then
   CONTINUE again), *Uploading (n%)*, then **"Mod uploaded with Unlisted visibility."** Press CONTINUE and the Steam overlay
   opens the new item's page. Copy that page's URL/number somewhere.
5. On the Workshop item page (Steam overlay, or in a browser while logged in):
   - **Description:** paste the contents of `workshop/description.bbcode.txt`. (The uploader wrote the plain `modinfo.json`
     text there; the BBCode version is nicer.)
   - Add a couple of screenshots of the options screen (Add Sound tab) and the demo video from the README if you like.
   - **Visibility stays "Unlisted" for now** (only people with the link can see it - nothing is public yet).
6. **Test the real install.** Unlisted items can still be subscribed to by link. To test what a player gets, make sure the
   local copy doesn't shadow it: if the local `mods\dion_soundboard` folder exists Remix ignores the subscribed copy with
   the same id. Rename that folder (e.g. to `dion_soundboard.off`), subscribe, let Steam download, start the game (Steam
   installs it under `steamapps\workshop\content\312520\<item number>\`), enable it in Remix, and check: mod appears with the
   thumbnail, sounds play, OPEN FOLDER opens the `Soundboard` data folder, and `BepInEx\LogOutput.log` has no errors.
   Then put your local folder back / unsubscribe.
7. When happy, on the item page change **Visibility to Public**.

## Publish an update

1. Bump the version in `mod/modinfo.json`, `[BepInPlugin]` in `src/Plugin.cs` and tag the release (that's the usual release
   routine), rebuild, commit the DLL.
2. Repeat step 1 of "first time" (clean the installed folder, `./scripts/deploy.ps1 -SkipBuild`).
3. Launch from Steam, Remix, select the mod, press the upload button, UPLOAD. The uploader finds your existing item by its
   `id` tag and updates it **in place**. The visibility you set is kept (only new items are forced to Unlisted).
4. **Then re-paste the BBCode description.** Every upload resets the item's title, description, tags and preview image to
   what is in `modinfo.json` / `thumbnail.png`, so the plain text description comes back each time. (If that gets
   annoying, that is the reason to keep `modinfo.json`'s description good on its own - it is written to read fine alone.)
5. Optionally write change notes on the item's **Change Notes** tab; the game uploads an empty change note.
6. Players who are subscribed get the update on their next Steam sync. Their `soundboard.yaml` and sounds are in the data
   folder, so nothing they made is touched.

## About `workshopid.txt`

Rain World's uploader does **not** create, read or need a `workshopid.txt` (the decompiled game code never mentions one).
The link between this mod and its Workshop item is kept in Steam: the item carries a hidden `id` tag equal to
`dion_soundboard`, and that is how the game finds it again. If you ever see a `workshopid.txt` mentioned elsewhere it is
a convention of other tools, not of this game. **Don't create or commit one.** The item's numeric id is public (it's in the
Workshop URL), so it is fine to note it in the README or here if you want a link, but nothing depends on it.

## Things to know

- The mod already keeps everything a player edits (`soundboard.yaml`, own sounds, backups, `events.txt`) in the game's data
  folder rather than the mod folder, so Steam replacing the mod folder on update is safe.
- Steam Workshop items are covered by the Workshop legal agreement: you are asserting you have the right to distribute
  what's in the folder. That is why the example sounds matter.
- To take it down later: on the item page, set Visibility to Private (or delete the item). It doesn't touch anything
  on players' machines other than removing the download.
