// Run after editing a downloadable example; the site itself requires no build.
import { readFile, writeFile } from "node:fs/promises";
const root = new URL("../", import.meta.url);
const pageURL = new URL("index.html", root);
let html = await readFile(pageURL, "utf8");
for (const { name, path, language, id } of [
  { name: "HypelineActionDemo.cs", path: "examples/HypelineActionDemo.cs", language: "csharp" },
  { name: "HypelineCodeDemo.cs", path: "examples/HypelineCodeDemo.cs", language: "csharp" },
  { name: "install-hypeline.md", path: "prompts/install-hypeline.md", language: "markdown", id: "agent-install-prompt" }
]) {
  const code = (await readFile(new URL(path, root), "utf8")).trimEnd();
  const escaped = code.replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;");
  const start = `<!-- example:${name} -->`;
  const end = `<!-- /example:${name} -->`;
  const from = html.indexOf(start);
  const to = html.indexOf(end);
  if (from < 0 || to < from) throw new Error(`Missing example markers for ${name}`);
  html = html.slice(0, from + start.length) + `<pre><code${id ? ` id="${id}"` : ""} class="language-${language}">${escaped}</code></pre>` + html.slice(to);
}
if (process.argv.includes("--check")) {
  if (html !== await readFile(pageURL, "utf8")) throw new Error("Embedded examples are stale; run sync-examples.mjs");
  console.log("Embedded examples and install prompt match downloadable files.");
} else {
  await writeFile(pageURL, html, "utf8");
  console.log("Updated embedded examples and install prompt.");
}
