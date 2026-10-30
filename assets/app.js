"use strict";

// Muxy docs use one dark theme.
document.documentElement.dataset.theme = "dark";

const menu = document.querySelector("#menu-toggle");
const sidebar = document.querySelector("#sidebar");
function closeMenu() { sidebar.classList.remove("open"); menu.setAttribute("aria-expanded", "false"); }
menu.addEventListener("click", () => {
  const open = sidebar.classList.toggle("open");
  menu.setAttribute("aria-expanded", String(open));
});
sidebar.querySelectorAll("a").forEach(link => link.addEventListener("click", closeMenu));
document.addEventListener("click", event => {
  if (!sidebar.contains(event.target) && !menu.contains(event.target)) closeMenu();
});
document.addEventListener("keydown", event => { if (event.key === "Escape") closeMenu(); });

const sections = [...document.querySelectorAll("main > section[id]")];
const links = [...document.querySelectorAll("#guide-nav a[href^='#']")];
let scrollScheduled = false;
function updateNavigation() {
  const active = sections.filter(section => section.getBoundingClientRect().top <= 140).at(-1) || sections[0];
  links.forEach(link => {
    const current = link.hash === `#${active.id}`;
    link.classList.toggle("active", current);
    if (current) link.setAttribute("aria-current", "location");
    else link.removeAttribute("aria-current");
  });
  scrollScheduled = false;
}
window.addEventListener("scroll", () => {
  if (!scrollScheduled) { scrollScheduled = true; requestAnimationFrame(updateNavigation); }
}, { passive: true });
updateNavigation();

let toastTimeout;
function toast(message) {
  const element = document.querySelector("#toast");
  element.textContent = message;
  element.classList.add("visible");
  clearTimeout(toastTimeout);
  toastTimeout = setTimeout(() => element.classList.remove("visible"), 2400);
}
document.querySelectorAll("pre").forEach(pre => {
  const code = pre.querySelector("code");
  if (!code) return;
  const button = document.createElement("button");
  button.type = "button";
  button.className = "copy-button";
  button.textContent = "Copy";
  button.setAttribute("aria-label", "Copy code example");
  button.addEventListener("click", async () => {
    try {
      await navigator.clipboard.writeText(code.textContent);
      button.textContent = "Copied";
      toast("Code copied to clipboard");
      setTimeout(() => { button.textContent = "Copy"; }, 2000);
    } catch { toast("Copy unavailable. Select the code and copy it manually."); }
  });
  pre.append(button);
});

const dialog = document.querySelector("#search-dialog");
const input = document.querySelector("#search-input");
const resultBox = document.querySelector("#search-results");
const searchIndex = sections.map(section => ({
  id: section.id,
  title: section.querySelector("h1, h2").textContent.replace(/\s+/g, " "),
  text: [...section.querySelectorAll("p, li, h3, td")]
    .map(element => element.textContent.replace(/\s+/g, " ")).join(" ")
}));
function renderSearch() {
  const query = input.value.trim().toLowerCase();
  const words = query.split(/\s+/).filter(Boolean);
  const matches = searchIndex.filter(item => words.every(word =>
    `${item.title} ${item.text}`.toLowerCase().includes(word)));
  resultBox.replaceChildren();
  document.querySelector("#search-count").textContent = query ?
    `${matches.length} matching section${matches.length === 1 ? "" : "s"}` : "Browse guide sections";
  matches.forEach(item => {
    const link = document.createElement("a");
    link.href = `#${item.id}`;
    const title = document.createElement("strong");
    title.textContent = item.title;
    const excerpt = document.createElement("p");
    const position = Math.max(0, item.text.toLowerCase().indexOf(words[0] || "") - 45);
    excerpt.textContent = (position ? "…" : "") + item.text.slice(position, position + 150) + "…";
    link.append(title, excerpt);
    link.addEventListener("click", () => {
      dialog.close();
      const heading = document.querySelector(`#${item.id} h1, #${item.id} h2`);
      heading.tabIndex = -1;
      heading.focus({ preventScroll: true });
    });
    resultBox.append(link);
  });
}
function openSearch() { closeMenu(); dialog.showModal(); renderSearch(); input.focus(); }
document.querySelector(".search-trigger").addEventListener("click", openSearch);
document.querySelector("#search-close").addEventListener("click", () => dialog.close());
input.addEventListener("input", renderSearch);
input.addEventListener("keydown", event => {
  if (event.key === "ArrowDown") { event.preventDefault(); resultBox.querySelector("a")?.focus(); }
  if (event.key === "Enter") resultBox.querySelector("a")?.click();
});
document.addEventListener("keydown", event => {
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
    event.preventDefault();
    if (!dialog.open) openSearch();
  }
});
dialog.addEventListener("click", event => {
  const bounds = dialog.getBoundingClientRect();
  if (event.clientX < bounds.left || event.clientX > bounds.right ||
      event.clientY < bounds.top || event.clientY > bounds.bottom) dialog.close();
});

// Enlarge tutorial screenshots while keeping ordinary image links as a fallback.
const screenshotLinks = document.querySelectorAll(".capture-zoom");
if (screenshotLinks.length && typeof HTMLDialogElement !== "undefined" && HTMLDialogElement.prototype.showModal) {
  const viewer = document.createElement("dialog");
  viewer.className = "image-viewer";
  viewer.setAttribute("aria-label", "Enlarged tutorial screenshot");
  viewer.innerHTML = '<div class="image-viewer-header"><p>Full-size image · Scroll to see more</p><button type="button">Close</button></div><div class="image-viewer-content"><img alt=""></div>';
  document.body.append(viewer);
  const viewerImage = viewer.querySelector("img");
  viewer.querySelector("button").addEventListener("click", () => viewer.close());
  viewer.addEventListener("click", event => { if (event.target === viewer) viewer.close(); });
  screenshotLinks.forEach(link => link.addEventListener("click", event => {
    if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
    event.preventDefault();
    viewerImage.src = link.href;
    viewerImage.alt = link.querySelector("img").alt;
    viewer.querySelector(".image-viewer-content").scrollTo(0, 0);
    viewer.showModal();
  }));
}
