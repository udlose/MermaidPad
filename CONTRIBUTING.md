# CONTRIBUTING to MermaidPad

Thank you for your interest in contributing to **MermaidPad**!
Your time and effort are valued — and contributors of all experience levels are welcome. 🎉🚀

This document outlines how to contribute effectively and consistently.

---

# 📌 Table of Contents

* [Getting Started](#getting-started)
* [Development Environment](#development-environment)
* [Branching & Workflow](#branching--workflow)
* [Coding Guidelines](#coding-guidelines)
* [Cross-Platform Responsibility](#cross-platform-responsibility)
* [Async & UI Thread Expectations](#async--ui-thread-expectations)
* [Testing Requirements](#testing-requirements)
* [Pull Requests](#pull-requests)
* [Issue Reporting](#issue-reporting)
* [Commit Messages](#commit-messages)
* [Code of Conduct](#code-of-conduct)

---

# Getting Started

Before working on the project:

1. Familiarize yourself with MermaidPad’s purpose and features.
2. Read through this file and the **README.md**.
3. Review open issues or Discussions to avoid duplicated work.

If you’re unsure where to start, feel free to open a Discussion — we’re friendly!

---

# Development Environment

MermaidPad requires:

* **.NET 9 SDK**
* **Avalonia** as the UI framework
* **ESLint 9+** for validating embedded JavaScript assets (when applicable)

No Node.js runtime is required unless you are modifying JavaScript packages that themselves require it.

---

# Branching & Workflow

To keep history clean and conflict-free:

1. Base new branches on **develop**
2. Use **git rebase** instead of merge when updating your branch
3. All pull requests must target **develop**

```
main → stable releases  
develop → active development  
feature/* → individual contribution work
bugfix/* → individual contribution work
```

---

# Coding Guidelines

### ✔ Follow existing patterns

Maintain code style consistency unless:

* A pattern is provably flawed, or
* You have a clear reason to improve it.

If you spot inconsistencies or technical debt, feel free to open an issue or Discussion.

### ✔ Use async when appropriate

Prefer async I/O and background work to avoid blocking the UI.

### ✔ Avoid unnecessary allocations and excessive use of LINQ in performance-critical UI paths

The app is cross-platform, and some platforms have more constrained environments.

---

# Cross-Platform Responsibility

MermaidPad runs on **Windows, macOS, and Linux**.
Avoid using OS-specific APIs directly in application code.

If OS-specific functionality is required, route it through:

* `WindowsPlatformServices`
* `LinuxPlatformServices`
* `MacPlatformServices`

These are located under `Services/Platforms`.

This keeps platform differences contained and prevents regressions.

---

# Async & UI Thread Expectations

This is important:

**Any code that interacts with UI or ViewModel objects must run on the UI thread.**

Examples in the project demonstrate:

* Posting UI updates from background operations
* Dispatching async callbacks correctly
* Avoiding deadlocks on Avalonia’s UI scheduler

If you’re unsure, search the codebase for existing async patterns and match them.

---

# Testing Requirements

All changes must be tested using both:

1. **DEBUG build**
2. **RELEASE build**

Release builds behave differently (trimming, optimizations, etc.) and can reveal issues that debug builds hide.

Instructions for building publishable artifacts are in **README.md → Building & Publishing**.

Additionally:

* Test on at least one desktop OS
* If you touched platform-specific code, test on multiple OSes where possible

---

# Pull Requests

Before submitting a PR:

### ✔ Ensure you have rebased your branch onto `develop`

No merge commits, please.

### ✔ Review warnings related to **your** C# changes

If your modification triggers new Roslyn warnings, address them before opening the PR.

### ✔ If you modified JavaScript, run ESLint

ESLint v9+ should report zero errors or warnings.

### ✔ Confirm builds succeed in both Debug and Release

### ✔ Keep PRs focused

Small, single-responsibility PRs are easier to review and merge.

### ✔ Include screenshots for UI changes

Visual changes are much easier to evaluate when screenshots are provided.

---

# Issue Reporting

When reporting an issue:

* Provide OS version
* Provide CPU architecture
* Include steps to reproduce
* Include logs if applicable
* Add screenshots when relevant
* Indicate whether the issue is reproducible (always, intermittent, rare, etc.)

The issue templates in `.github/ISSUE_TEMPLATE/` will guide you.

---

# Commit Messages

> [!IMPORTANT]
> - Every commit message should reference the Issue it relates to.
> - Avoid vague messages like "Fix bug" or "Update code".

Clear commit messages help maintain a readable history.

You can do either of the following:
- Use AI generated commit messages based on your changes
- or follow this pattern:

  ```
  Short summary of the change

  Issue #<issue-number>

  Detailed explanation (optional)
  - Why the change was made
  - What problem it solves
  - Relevant context or links
  ```

Examples:

* `Fix: handle large mermaid diagrams without UI freeze. Resolves Issue #123`
* `Refactor: consolidate Linux clipboard integration. Resolves Issue #456`
* `Feature: improved tab persistence across sessions. Resolves Issue #789`
---

# Code of Conduct

Be kind, be respectful, and act professionally.
We’re building a community together — empathy and clarity matter. See the [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) for details.

---

# Thank You

Your contributions — small or large — help improve MermaidPad for everyone.
Thank you again for helping make the project better! 🙌

---

