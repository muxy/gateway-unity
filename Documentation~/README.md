# Muxy Hypeline Unity developer documentation

A static, dependency-free documentation page for GitHub Pages. It covers installing
the current SDK using the existing Muxy Gateway service and extension, prefab scene setup, streamer authentication, viewer actions, live
game data, polling, an alternative C# integration, testing, and troubleshooting.
The walkthrough also covers adding and wiring the Hypeline menu and interaction
alert prefabs, connecting Twitch, and testing a viewer action.

The `~` suffix keeps Unity from importing the website and tutorial C# files as
package assets. Copy an example into a consuming project's `Assets/Scripts` to use
it. Existing Gateway API names in the examples remain supported; new code can also
construct `MuxyHypeline.SDK` using the same API and `MuxyGateway` data types.

## Preview locally

Serve this folder with any static HTTP server. For example, from the repository
root, with Python 3 installed:

```sh
python -m http.server 4173 --bind 127.0.0.1 --directory 'Documentation~'
```

Open <http://localhost:4173>. The written guide, navigation links, and embedded
examples remain readable without JavaScript; search, theme switching, code copying,
and registered screenshot loading use JavaScript. All asset links are relative so
the page works at a GitHub Pages project path as well as a custom domain.

## Publish on GitHub Pages

1. Add these files and `.github/workflows/docs-pages.yml` to the repository.
2. In the repository's **Settings → Pages → Build and deployment**, set **Source**
   to **GitHub Actions**. Ensure Pages is enabled for your repository/plan.
3. Merge the documentation into `main`. The supplied workflow deploys when the
   documentation changes on `main`. You can also run **Deploy Unity SDK
   documentation** manually from the Actions tab after the workflow exists on the
   default branch. If publishing from a different branch, update the workflow's
   branch filter and the `github-pages` environment's allowed deployment branches.
4. Read the deployed URL from the successful job's `github-pages` environment.

The workflow uploads only `Documentation~`, verifies the embedded examples match
their downloads, and uses the standard Pages environment and permissions. It does
not require an npm install or a site generator. This delivery has not enabled Pages,
pushed a branch, or deployed the site.

Reference: [GitHub's custom Pages workflow documentation](https://docs.github.com/en/pages/getting-started-with-github-pages/using-custom-workflows-with-github-pages).

## Edit the page

- `index.html`: the complete guide and numbered screenshot slots.
- `assets/styles.css`: responsive layout, light/dark theme, and print styles.
- `assets/app.js`: local guide search, copy buttons, navigation, and screenshot loading.
- `examples/*.cs`: complete tutorial behaviors, downloadable from the guide.
- `SCREENSHOTS.md`: the requested captures and image integration instructions.
- `assets/screenshots.json`: approved images to replace numbered placeholders.

After editing either C# file, run:

```sh
node 'Documentation~/tools/sync-examples.mjs'
node 'Documentation~/tools/sync-examples.mjs' --check
```

Do not edit only the HTML copy of a full example. For site checks, verify local
links/anchors, both download links, mobile navigation, search, code copying, theme
switching, and absence of console errors. C# compilation proves API/syntax
compatibility only. Live PIN authentication and a no-vote poll callback were
checked in a Unity 6000.6.6f1 Windows test project; the saved-token login also
succeeded on the next Play run. A viewer action, viewer vote, and player build
still need verification.

## Content scope

The guide reflects the local compatibility checkout, not a claim that a historical
release ZIP contains those updates. It explicitly notes existing manager logout,
polling UI, notification UI, and transport lifecycle limits found in the source.
Before publishing as release documentation, choose the shipping tag/commit and
reconcile these notes with that revision. No live PINs or credentials are needed
to edit, preview, or host this page.
