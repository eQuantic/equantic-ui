// The pipelines board's page. Every text is set with textContent; a link is built only from an
// integer and the repository's GitHub address the server parsed from the `origin` remote.
"use strict";

const POLL_MS = 3000;
// `busy` keeps one poll loop however often the tab is hidden and shown; `ticket` drops a report's
// answer that arrives after another report was asked for; `reportsKey` keeps the open report's <pre>
// (its scroll) across polls.
const view = {
  tab: "running", key: null, stage: {}, report: null, reportText: "", data: null,
  timer: null, busy: false, ticket: 0, reportsKey: null,
};

const ICONS = {
  completed: "✓", done: "✓", in_progress: "◐", running: "◐", pending: "○", waiting: "◷",
  shipping: "◷", failed: "✕", broken: "✕", not_applicable: "–",
};
const WORDS = {
  completed: "completed", done: "done", in_progress: "in progress", running: "running", pending: "pending",
  waiting: "waiting", shipping: "shipping", failed: "failed", broken: "unreadable", not_applicable: "not applicable",
};

function el(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined && text !== null) node.textContent = String(text);
  return node;
}

function add(parent, ...children) {
  for (const child of children) if (child) parent.append(child);
  return parent;
}

const known = (status) => (Object.prototype.hasOwnProperty.call(WORDS, status) ? status : "unknown");

function pill(status, label) {
  const kind = known(status);
  const node = el("span", "pill " + kind);
  const icon = el("span", "icon" + (kind === "in_progress" || kind === "running" ? " spin" : ""), ICONS[kind] || "?");
  icon.setAttribute("aria-hidden", "true");
  return add(node, icon, el("span", null, label || WORDS[kind] || String(status)));
}

const number = (n) => (typeof n === "number" ? n.toLocaleString("en-US") : "—");

function duration(seconds) {
  if (typeof seconds !== "number" || !isFinite(seconds)) return "—";
  const s = Math.max(0, Math.round(seconds));
  const h = Math.floor(s / 3600), m = Math.floor((s % 3600) / 60), r = s % 60;
  if (h) return `${h}h ${String(m).padStart(2, "0")}m`;
  if (m) return `${m}m ${String(r).padStart(2, "0")}s`;
  return `${r}s`;
}

function link(kind, n) {
  const repo = view.data && view.data.repo;
  if (!Number.isInteger(n) || n <= 0 || typeof repo !== "string" || !/^https:\/\/github\.com\/[\w.-]+\/[\w.-]+$/.test(repo)) {
    return null;
  }
  const a = el("a", null, `${kind === "pull" ? "PR" : "Issue"} #${n}`);
  a.href = `${repo}/${kind}/${n}`;
  a.target = "_blank";
  a.rel = "noopener noreferrer";
  return a;
}

function text(value) {
  if (value === null || value === undefined) return "";
  if (typeof value !== "object") return String(value);
  if (Array.isArray(value)) return value.map(text).join(", ");
  return Object.entries(value).map(([k, v]) => `${k}: ${text(v)}`).join(" · ");
}

// -------------------------------------------------------------------------------------- polling

async function poll() {
  if (view.busy) return;
  clearTimeout(view.timer);
  view.timer = null;
  if (document.hidden) return;
  view.busy = true;
  try {
    const response = await fetch("/api/pipelines", { cache: "no-store" });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    view.data = await response.json();
    render();
    document.getElementById("updated").textContent = `Updated ${new Date().toLocaleTimeString()}`;
  } catch (error) {
    document.getElementById("updated").textContent = `Not updated: ${error.message}`;
  } finally {
    view.busy = false;
    clearTimeout(view.timer);
    view.timer = document.hidden ? null : setTimeout(poll, POLL_MS);
  }
}

document.addEventListener("visibilitychange", () => {
  if (document.hidden) {
    clearTimeout(view.timer);
    view.timer = null;
  } else {
    poll(); // does nothing while a request is in flight: its end schedules the next one
  }
});

for (const tab of document.querySelectorAll("[data-tab]")) {
  tab.addEventListener("click", () => {
    view.tab = tab.dataset.tab;
    for (const other of document.querySelectorAll("[data-tab]")) other.setAttribute("aria-selected", String(other === tab));
    render();
  });
}

// ------------------------------------------------------------------------------------- rendering

function shown() {
  const all = (view.data && view.data.deliveries) || [];
  return view.tab === "running" ? all.filter((d) => d.status === "running") : all;
}

function select(key) {
  view.key = key;
  view.report = null;
  view.reportText = "";
  view.ticket += 1;
}

// A poll rebuilds the list and the detail: the control that had the focus gets it back by its
// `data-focus` name, so the page stays usable from the keyboard while it polls.
function render() {
  const active = document.activeElement;
  const focused = active && active.dataset ? active.dataset.focus : undefined;
  const deliveries = shown();
  if (!deliveries.some((d) => d.key === view.key)) select(deliveries.length ? deliveries[0].key : null);
  const d = deliveries.find((x) => x.key === view.key);
  renderKpis(deliveries);
  renderList(deliveries);
  renderDetail(d);
  renderReports(d);
  if (focused) {
    const target = Array.from(document.querySelectorAll("[data-focus]")).find((e) => e.dataset.focus === focused);
    if (target && target !== document.activeElement) target.focus({ preventScroll: true });
  }
}

function renderKpis(deliveries) {
  const all = view.data.deliveries;
  const agents = all.reduce((n, d) => n + ((d.agents && d.agents.length) || 0), 0);
  const tiles = [
    ["Deliveries running", number(all.filter((d) => d.status === "running").length), `${all.length} on disk`],
    ["Agents working now", number(agents), view.data.registry ? "live sessions only" : "liveness unknown: no session registry"],
    ["Tokens", number(deliveries.reduce((n, d) => n + (d.tokens || 0), 0)), view.tab === "running" ? "of the running deliveries" : "of every delivery"],
    ["Wall time", duration(deliveries.reduce((n, d) => n + (d.seconds || 0), 0)), "summed over the deliveries shown"],
  ];
  const root = document.getElementById("kpis");
  root.replaceChildren(...tiles.map(([label, value, hint]) =>
    add(el("div", "card kpi"), el("div", "label", label), el("div", "value", value), el("div", "hint muted", hint))));
}

function renderList(deliveries) {
  const root = document.getElementById("list");
  if (!deliveries.length) {
    root.replaceChildren(el("p", "empty", view.tab === "running" ? "No delivery is running." : "No delivery on disk."));
    return;
  }
  root.replaceChildren(...deliveries.map((d) => {
    const item = el("button", "item");
    item.type = "button";
    item.dataset.focus = `delivery:${d.key}`;
    item.setAttribute("aria-current", String(d.key === view.key));
    item.addEventListener("click", () => {
      select(d.key);
      render();
    });
    const agents = (d.agents && d.agents.length) || 0;
    return add(item,
      add(el("div", "row"), el("span", "name", d.key), pill(d.status)),
      el("div", "meta", `${number(d.tokens)} tokens · ${duration(d.seconds)}${agents ? ` · ${agents} working` : ""}`));
  }));
}

function renderDetail(d) {
  const root = document.getElementById("detail-main");
  if (!d) {
    root.replaceChildren();
    return;
  }
  if (d.status === "broken") {
    root.replaceChildren(add(el("div", "card"), add(el("div", "head"), el("h2", null, d.key), pill("broken")),
      el("p", "error", `state.json could not be read: ${d.error}`)));
    return;
  }
  const stages = d.stages || [];
  const picked = stages.find((s) => s.name === view.stage[d.key]) || stages.find((s) => s.status === "in_progress") || stages[stages.length - 1];
  root.replaceChildren(...[summary(d), graph(d, picked), agentsCard(d), stepsCard(d, picked), notesCard(d, picked)].filter(Boolean));
}

function summary(d) {
  const card = el("div", "card");
  const head = add(el("div", "head"), el("h2", null, Number.isInteger(d.issue) ? `#${d.issue}` : d.key), pill(d.status));
  if (d.high_effort) add(head, pill("effort", "high effort"));
  add(head, el("span", "branch", d.branch || ""));
  add(head, add(el("span", "links"), link("issues", d.issue), link("pull", d.pull_request)));
  add(card, head);
  if (d.high_effort && d.high_effort_reason) add(card, el("p", "reason", d.high_effort_reason));
  const facts = el("div", "facts");
  for (const [label, value] of [["Pipeline", d.pipeline || "—"], ["Tokens", number(d.tokens)], ["Time", duration(d.seconds)],
    ["Started", d.started || "—"], ["Worktree", d.worktree]]) {
    add(facts, add(el("span"), el("span", null, `${label} `), el("strong", null, value)));
  }
  return add(card, facts);
}

function node(name, status, nums, agent, onClick, pressed) {
  const box = el(onClick ? "button" : "div", `node ${known(status)}`);
  if (onClick) {
    box.type = "button";
    box.dataset.focus = `stage:${name}`;
    box.setAttribute("aria-pressed", String(Boolean(pressed)));
    box.addEventListener("click", onClick);
  }
  return add(box, el("span", "label", name), pill(status), nums ? el("span", "nums", nums) : null, agent ? el("span", "agent", agent) : null);
}

function graph(d, picked) {
  const card = add(el("div", "card"), el("h2", null, "Pipeline"));
  const row = el("div", "graph");
  add(row, node("Intake", "completed", null, "the session"));
  for (const s of d.stages) {
    const who = [s.agent, s.model, s.effort, s.in_state ? null : "recorded in measures only"].filter(Boolean).join(" · ");
    add(row, node(s.name, s.status, `${duration(s.seconds)} · ${number(s.tokens)} tokens`, who,
      () => { view.stage[d.key] = s.name; render(); }, picked && picked.name === s.name));
  }
  if (d.ship) {
    const ship = d.ship;
    const parts = [ship.merged ? `merged ${ship.merged}` : ship.queued ? `queued ${ship.queued}` : ship.pr_opened ? `opened ${ship.pr_opened}` : null,
      ship.note, ship.queue_exits && ship.queue_exits.length ? `${ship.queue_exits.length} queue exit(s)` : null,
      ship.last_ci ? `last CI ${text(ship.last_ci)}` : null].filter(Boolean);
    add(row, node("Ship", ship.status, parts.join(" · ") || null, "the session"));
  }
  return add(card, row);
}

function agentsCard(d) {
  const agents = d.agents || [];
  const card = add(el("div", "card"), el("h2", null, `Agents working now (${agents.length})`));
  if (!agents.length) return add(card, el("p", "muted", "No agent is working on this delivery."));
  const list = el("div", "agents");
  for (const a of agents) {
    const facts = el("div", "facts muted");
    for (const value of [a.model && `${a.model}${a.effort ? ` · ${a.effort}` : ""}`, a.stage && `stage ${a.stage}`,
      a.context !== null && a.context !== undefined && `context now ${number(a.context)}`,
      `running for ${duration(a.running_for)}`, `last activity ${duration(a.idle_for)} ago`,
      a.live === null ? "liveness unknown" : null]) {
      if (value) add(facts, el("span", null, value));
    }
    add(list, add(el("div", "agent-card"),
      add(el("div", "head"), pill("in_progress", a.type || "agent"), el("span", null, a.description || a.id)),
      el("div", "doing", a.doing || "no tool yet"), facts));
  }
  return add(card, list);
}

function stepsCard(d, s) {
  if (!s) return null;
  const card = add(el("div", "card"), add(el("div", "head"), el("h2", null, `Steps of ${s.name}`), pill(s.status)));
  if (!s.steps.length) return add(card, el("p", "muted", "No step recorded yet."));
  const table = el("table");
  const head = el("tr");
  for (const [label, num] of [["Step", false], ["Tokens", true], ["Tool uses", true], ["Duration", true], ["Notes", false]]) {
    add(head, el("th", num ? "num" : null, label));
  }
  add(table, add(el("thead"), head));
  const body = el("tbody");
  for (const step of s.steps) {
    add(body, add(el("tr"), el("td", null, step.name), el("td", "num", number(step.tokens)), el("td", "num", number(step.tool_uses)),
      el("td", "num", step.duration_ms === null ? "—" : duration(step.duration_ms / 1000)), el("td", "muted", text(step.notes))));
  }
  add(body, add(el("tr", "total"), el("td", null, "Total"), el("td", "num", number(s.tokens)), el("td", "num", number(s.tool_uses)),
    el("td", "num", duration(s.seconds)), el("td", "muted", s.report ? `report ${s.report}` : "")));
  add(table, body);
  add(card, add(el("div", "scroll"), table));
  if (s.report && (d.reports || []).includes(s.report)) {
    const open = el("button", null, `Open ${s.report}`);
    open.type = "button";
    open.dataset.focus = `open:${s.report}`;
    open.addEventListener("click", () => openReport(d, s.report));
    add(card, add(el("div", "reports"), open));
  }
  return card;
}

function notesCard(d, s) {
  const items = [];
  for (const n of (s && s.notes) || []) items.push(`${s.name} › ${n.path}: ${text(n.values)}`);
  for (const [name, values] of Object.entries(d.notes || {})) {
    for (const value of Array.isArray(values) ? values : [values]) items.push(`${name.replace(/_/g, " ")}: ${text(value)}`);
  }
  if (!items.length) return null;
  return add(el("div", "card"), el("h2", null, "Notes"), add(el("ul", "notes"), ...items.map((t) => el("li", null, t))));
}

// The reports are rebuilt only when what they show changes, never by a poll: the open report's
// <pre> keeps its scroll and its buttons their focus.
function renderReports(d) {
  const root = document.getElementById("detail-reports");
  const key = JSON.stringify([d ? d.key : null, d ? d.reports : null, view.report, view.reportText]);
  if (key === view.reportsKey) return;
  view.reportsKey = key;
  root.replaceChildren(...(d ? [reportsCard(d)] : []));
}

function reportsCard(d) {
  const card = add(el("div", "card"), el("h2", null, "Reports"));
  const names = d.reports || [];
  if (!names.length) return add(card, el("p", "muted", "No report in this delivery's folder."));
  const row = el("div", "reports");
  for (const name of names) {
    const button = el("button", null, name);
    button.type = "button";
    button.dataset.focus = `report:${name}`;
    button.setAttribute("aria-pressed", String(view.report === name));
    button.addEventListener("click", () => openReport(d, name));
    add(row, button);
  }
  add(card, row);
  if (view.report) add(card, el("pre", null, view.reportText));
  return card;
}

async function openReport(d, name) {
  const ticket = ++view.ticket;
  view.report = name;
  view.reportText = "Loading…";
  render();
  let answer;
  try {
    const response = await fetch(`/api/report/${encodeURIComponent(d.key)}/${encodeURIComponent(name)}`, { cache: "no-store" });
    answer = response.ok ? await response.text() : `Not available (HTTP ${response.status}).`;
  } catch (error) {
    answer = `Not available: ${error.message}`;
  }
  if (ticket !== view.ticket) return; // another report, or another delivery, was asked for since
  view.reportText = answer;
  render();
}

poll();
