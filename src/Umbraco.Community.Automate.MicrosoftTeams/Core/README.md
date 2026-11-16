Line-agnostic code lives here: it compiles unchanged on both Umbraco lines (`AutomateMajor` 17 and 18).
Anything that differs per Umbraco line goes in `Web/`, `Client/` or the build files, behind `#if AUTOMATE_17` / `#if AUTOMATE_18`.
