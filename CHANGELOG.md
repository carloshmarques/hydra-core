<!-- markdownlint-disable MD024-->
# **Change Log** 📜📝

All notable changes to the "**hydra-core**" project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [**Unreleased**]

### Added

* Flat root directory structure (`data/`, `src/`, `templates/`, `assets/`) and private workspace folders (`_data/`, `_docs/`, `_drafts/`).
* Standard `.gitattributes` file for cross-platform line-ending normalization.

### Changed

* Rewritten `.gitignore` entirely in English and configured it to ignore local workspace folders (`_data/`, `_docs/`, `_drafts/`) and `.sqlite` databases.
* Standardized `assets/img/` SVG filenames to English (`kebab-case`).

### Removed

* Deep nested `system/data/` directory structure and early SQLite database prototypes from version control.

---

## [**0.0.1**] - 2026-11-21

### Added

* The basic project structure from **[josee9988/project-template](https://github.com/Josee9988/project-template)**.
