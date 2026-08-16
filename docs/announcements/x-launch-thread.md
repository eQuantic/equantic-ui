# eQuantic.UI: launch thread for X

Companion to `reddit-launch-post.md`. Same facts, different medium: on X the first line and the
image decide everything, and nobody reads a paragraph they did not choose to open.

> **House style**: no em dashes. Use ":", "…", "()" or a full stop.
>
> The blocks below carry only DELIBERATE line breaks. X keeps every newline you paste, so a block
> hard-wrapped to look tidy in this file would arrive on a timeline broken mid-sentence.

---

## 0. How to post it

- **Attach `assets/post/hero.png` to post 1.** On X the image is the hook, not the text. Alt text is
  written out in section 3 below; fill it in, both because it is right and because it is indexed.
  The same file serves Reddit: it is 16:9, which is exactly what X shows for a landscape image, and
  rendered down to a 600px feed width the headline, both card headings and the footer claim stay
  readable (the code becomes texture, which is the right thing to lose first).
  What does break it is a SQUARE crop: centred, "One component class" arrives as "nent class." and
  the logo is gone. So never let a surface centre-crop it, and if one does, ask for a 1:1 or 4:5
  variant with the content re-laid out rather than shipping the beheaded version.
- **Keep post 1 free of links.** X ranks link posts lower, so the link goes in the last post of the
  thread, where the people who read that far are the ones who would click anyway.
- **Write the whole thread before posting.** Posting the first one and typing the rest live means the
  early readers see an orphan claim with no evidence under it.
- **Two hashtags at most, at the end: `#dotnet #csharp`.** More reads as reach-farming.
- **Stay for replies.** The comment answers in `reddit-launch-post.md` section 4 work here verbatim,
  just shorter.
- **When you have the GIF** (same component in a browser tab and a native window, edited once, hot
  reloading in both), post it as a reply to your own thread. It will outperform everything above it.

---

## 1. The thread

### 1/7

```
One C# class.
A web page, and a native GPU screen.

You write C#: no JavaScript, no CSS, no Node toolchain, no package.json.

eQuantic.UI, 0.2 preview, MIT, .NET 10.
```

*(attach hero.png)*

### 2/7

```
What the browser gets is an ordinary page: server-rendered, hydrated, split per route.

A ~110 KB runtime, not a megabyte one. No WASM boot.
```

### 3/7

```
There is no CSS to write.

Components declare typed values (spacing, color tokens, type roles) and the compiler lowers each one into a single atomic class, deduplicated app-wide.

Hover and focus become real pseudo-classes, so they cost zero JS.
```

### 4/7

```
On macOS, iOS and Android the same classes are drawn on the GPU by our own engine: Metal and Vulkan. No WebView. No Skia.

A CPU rasterizer is the ground truth, and both backends are pinned to it by golden images.
```

### 5/7

```
Hardware is not a per-platform branch.

Camera, location, biometrics, secure storage: services taken through a constructor, realized per host, and null where a host has none.

Secrets are a separate interface from ordinary storage, on purpose.
```

### 6/7

```
What it is not, yet:

0.2 preview, the API moves between releases.
No global state solution.
No server push.
Native means mobile plus macOS, no Windows or Linux shell.

I would not bet a product on it this month.
```

### 7/7

```
Two things you can check instead of taking my word for it:

The playground runs the real compiler in your browser, and ui.equantic.tech is itself built with the framework, from the published packages.

https://ui.equantic.tech/playground

#dotnet #csharp
```

Post 6 is the one that makes the rest believable, so do not drop it to shorten the thread. On a
timeline full of launches, the person who lists what their own thing cannot do is the one worth
reading.

---

## 2. If you would rather post once

```
One C# class. A web page and a native GPU screen.

You write C#: no JavaScript, no CSS, no Node toolchain. The browser still gets an ordinary server-rendered page, and mobile is drawn on the GPU by our own Metal/Vulkan engine.

0.2 preview, MIT.
ui.equantic.tech/playground
```

*(attach hero.png)*

The single post trades the honesty section for brevity, so if you use it, put the limits in the
first reply rather than leaving them out.

---

## 3. Alt text for hero.png

```
A dark slide titled "One component class. A web page and a native screen." On the left, C# source for a HomePage component: a [Page("/")] attribute, a _count field, and a Build method returning a Column with a Text and a Button. On the right, two cards. WEB: a server-rendered page that hydrates then routes on the client, split per route with atomic CSS, a ~110 KB runtime and no WASM boot. NATIVE: macOS, iOS and Android, drawn on the GPU by the project's own Metal and Vulkan engine, with camera, location, biometrics and secure storage injected, no WebView and no Skia. Footer: "C# in. No JavaScript, no CSS, no Node toolchain."
```

---

## 4. Order of channels

X first, Reddit after, and not in the same hour. The X thread is where the wording gets its first
test, and a reply there ("but how is this different from Blazor?") tells you which answer needs to
be in the Reddit post before you write it. Reddit is also the harsher room, so arriving with the
objections already rehearsed is worth the delay.
