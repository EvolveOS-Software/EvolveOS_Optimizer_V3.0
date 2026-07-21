<div align="center">
<img src="https://github.com/EvolveOS-Software/EvolveOS_Optimizer_V3.0/blob/master-net10.0/Assets/EvolveOS_Optimizer.png"/><br/>
<img src="https://github.com/EvolveOS-Software/EvolveOS_Optimizer_V3.0/blob/master-net10.0/.github/Screenshots/preview.gif"/><br/><br/>
 
<div align="center" style="margin: 20px 0; text-align: center;">
 
[![Latest Release](https://img.shields.io/github/v/release/EvolveOS-Software/EvolveOS_Optimizer_V3.0?style=for-the-badge&color=179962)](https://github.com/EvolveOS-Software/EvolveOS_Optimizer_V3.0/releases/latest)
![Downloads](https://img.shields.io/github/downloads/EvolveOS-Software/EvolveOS_Optimizer_V3.0/total.svg?style=for-the-badge&color=1982a5)
![Stars](https://img.shields.io/github/stars/EvolveOS-Software/evolveos_optimizer_v3.0?style=for-the-badge&color=179962)
![Size](https://img.shields.io/github/repo-size/EvolveOS-Software/evolveos_optimizer_v3.0?style=for-the-badge&color=1982a5)
[![BSD 3-Clause License](https://img.shields.io/badge/License-BSD%203--Clause-yellow.svg?style=for-the-badge&color=179962)](https://github.com/EvolveOS-Software/EvolveOS_Optimizer_V3.0/blob/master-net10.0/LICENSE)
</div>

<br/><a href="https://github.com/EvolveOS-Software/EvolveOS_Optimizer_V3.0/releases/latest/download/EvolveOS_Optimizer.exe"><img src="https://github.com/EvolveOS-Software/EvolveOS_Optimizer_V3.0/blob/master-net10.0/.github/button.png" width="260" height="68" alt="Download the latest version"></a><br/><br/>

**The ultimate open-source Windows 11 optimization suite, debloater, and privacy tweaker built with WinUI 3. A modern, self-contained Windows management and PC cleaner utility that combines aggressive system optimization, deep privacy controls, and military-grade security tools into a single lightweight executable.**

[![Platform](https://img.shields.io/badge/Platform-Windows%2011-0078D7.svg?style=flat-square&logo=windows)](https://github.com/EvolveOS-Software/EvolveOS_Optimizer_V3.0)
[![WinGet](https://img.shields.io/badge/WinGet-Validated%20&%20Safe-0078D7.svg?style=flat-square&logo=windows)](https://github.com/microsoft/winget-pkgs/tree/master/manifests/e/EvolveOS/Optimizer)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](https://opensource.org/licenses/MIT)
[![WinUI 3](https://img.shields.io/badge/UI-WinUI%203-blueviolet?style=flat-square)](#)

</div>

---

## 📖 Why Choose EvolveOS Over Other Windows Optimizers?
The inspiration for this project came from navigating the fragmented landscape of Windows optimizers and debloat scripts. While many tools serve a specific niche, I wanted to build a unified powerhouse that perfectly aligned with my vision of the **best WinUI 3 system utility**. The result is a comprehensive suite designed from the ground up to merge aggressive system optimization with a highly refined, modern user experience.

If you are looking for a **lightweight alternative to CCleaner or BleachBit**, EvolveOS Optimizer has evolved far beyond a simple debloat utility. It is a premium, open-source Windows management, security, and cryptographic suite. Built on the modern WinUI 3 and Windows App SDK frameworks, it runs entirely as a zero-footprint standalone executable—proudly engineered to deliver deep OS control without adding to system bloat.

Under the hood, it empowers users with features previously scattered across dozens of separate programs: a newly integrated, dedicated profile builder for both massive system **optimizations** and deep UI **customizations**, military-grade AES-256 encryption, a custom Windows 11 ISO builder, an integrated WinGet software store, and advanced registry startup management. This raw power is wrapped in a dynamic, drag-and-drop dashboard brought to life with smooth Fluent animations, full Light and Dark theme support, and customizable UI backdrops (Mica and Acrylic).

Whether you want to know **how to debloat Windows 11 safely**, drastically optimize your gaming performance, generate custom offline boot media, or let its Autopilot engine run silently in your system tray, EvolveOS Optimizer continuously defends your privacy, secures your data, and ensures your PC runs at absolute peak performance—all without ever interrupting your workflow.

## ✨ Key Features: Windows 11 Optimization, Customizations, Debloating & Security

* **Dedicated Optimization Profile Builder (The Bridge to Custom ISOs):** The Profile Builder is a highly advanced state-management engine that completely decouples your system tweaks from the live operating system. It allows you to visually construct, save, and deploy complex configuration states using a robust JSON backend.
  * **System Seeding:** With a single click, the engine queries your live Windows environment and "seeds" the builder with your exact current configuration, allowing you to instantly clone your host machine's state.
  * **Offline ISO Injection:** The Profile Builder is directly linked to the EvolveOS Custom Windows 11 ISO Builder. Instead of just tweaking your live OS, the engine compiles your selected UI customizations and performance optimizations into raw registry instruction sets (`reg.exe` commands). These instructions are then injected directly into your custom Windows ISO, ensuring your specific tweaks, power plans, and privacy rules are baked in *before* the operating system is even installed.
  * **Staged Previews & Live Application:** Import community-shared tweak profiles and safely view them in a "Staged Preview" mode. The engine highlights exactly what will change without altering your system. Once verified, use "Live Apply Mode" to deploy hundreds of tweaks to your active machine instantly.
  * **Quick Presets & State Recovery:** Features instant one-click applications for "All Recommended" or "All Default" settings. It utilizes a temporary state handoff (`EvolveOS_TempBuilderState.json`) to prevent data loss if the app is closed abruptly while you are crafting the perfect OS profile.
* **Dedicated Optimization Pages & Badge System:** Navigate through completely new, categorized pages for every aspect of your OS. Explore dedicated hubs for System Optimizations (Power, Updates, Network) and UI Customizations (Explorer, Taskbar, Start Menu). Every page features an innovative new badge system, allowing you to visually track which settings you have modified at a glance.
* **Advanced Gaming & Performance Optimizations:** Push your hardware further by enforcing strict CPU and GPU priority scheduling for games, disabling Multi-Plane Overlay (MPO) to fix screen stuttering, fine-tuning Svchost split thresholds, disabling Dynamic Tick for lower DPC latency, and optimizing network throttling.
* **Power & Update Optimizations:** Take absolute control over Windows Updates by enforcing strict update policies, deferring feature updates, and utilizing the advanced Point-in-Time Restore backup system to roll back critical system changes. Maximize hardware efficiency with advanced PCIe Link State power management, deep hibernation controls, and processor boost thresholds.
* **Sound & Notification Mastering:** Configure automatic audio ducking preferences, restore legacy Windows startup sounds, and precisely control which system notifications, suggestions, and reminders are allowed to appear on your lock screen and taskbar.
* **Deep Windows Customizations:** Tailor your OS experience with deep UI and behavioral tweaks. Restore classic context menus, modify File Explorer shortcut behaviors, take control of the Start Menu layout, seamlessly adjust taskbar alignment and search box styles, and force system-wide Light, Dark, and Acrylic transparency themes.
* **Next-Gen Privacy & AI Controls:** Go beyond basic telemetry blocking. EvolveOS Optimizer allows you to completely disable intrusive Windows AI features including Copilot, Recall, and Click to Do, while providing granular control over User Account Control (UAC), camera/microphone access, and background app permissions.
* **Zero-Footprint Architecture:** As a system optimizer, the app itself is engineered to consume as little memory as possible. It utilizes a strict zero-cache navigation model where every page implements a custom `IPurgeable` interface. ViewModels are aggressively disposed, background threads are cancelled, and UI elements are completely purged from memory the moment you navigate away—ensuring perfectly fluent browsing with zero passive memory bloat.
* **Global Optimization Hotkeys:** Configure custom keyboard shortcuts to instantly flush system memory, clear DNS cache, and wipe temp files at any time. Triggering this directly optimizes the app itself—drastically reducing active memory usage when the dashboard is open, and aggressively trimming its background footprint down to an ultra-light 35-50 MB when hidden in the system tray.
* **Diagnostics Command Center:** The real-time nerve center of EvolveOS Optimizer. Powered by a custom Neural Analysis Engine, this dynamic dashboard aggregates critical system telemetry, real-time Plug and Play (PnP) hardware events, and network intelligence into a single, thread-safe interface. It runs continuous background tracking even when minimized, filtering out minor system noise to highlight actionable data. It goes beyond passive monitoring by offering contextual administrative elevation, allowing you to instantly execute remediation tasks and system repairs directly from the diagnostic event list.
* **Next-Gen Registry Editor (With AI & RegShot):** A high-performance, WinUI 3-native Registry Editor engineered for power users. It features a modern multi-tab workspace for simultaneous hive navigation, a blazing-fast custom hierarchical TreeView, and a recursive Global Search Engine. Built with absolute safety in mind, it includes a native permissions manager to securely elevate and temporarily bypass TrustedInstaller locks, while a custom `RegistryTransactionManager` acts as a safety net by providing full 'Undo' functionality for all your edits. To lower the barrier of entry, it integrates contextual "Ask AI" analysis to explain complex registry keys, and a built-in Registry Snapshot Engine to track and diff system modifications in real-time.
* **Live Telemetry & Interactive Graphing:** A high-performance, real-time metrics engine tracking CPU, RAM, Disk I/O, Pagefile, GPU Engine utilization, hardware thermals (temperatures), and dual-band Network speeds. Features a beautifully rendered, interactive history graph with dynamic time scales (ranging from 60 seconds up to 15 minutes) powered by an optimized, 900-point rolling memory buffer.
* **Native Fan Control & Unified Thermal Engine:** Eliminate heavy, privacy-invasive OEM bloatware (like Armoury Crate or iCUE) with a lightweight, built-in cooling management hub. 
  * **Interactive Fan Curve Graphing:** Build your perfect cooling curve with 4 to 8 draggable control points, dynamic gap interpolation, and multi-source thermal mapping (bind individual fans to CPU, GPU, Motherboard, or RAM temperatures).
  * **Global Presets & Advanced Tuning:** Instantly apply system-wide profiles (Silent, Balanced, Performance) with a single click. Power users can utilize Split Response Times (independent ramp-up/ramp-down hysteresis) and an automated Fan Calibration tool to detect precise motor spin-up thresholds.
  * **Integrated Thermal Safeguards:** Configure emergency warning limits, shutdown thresholds, and hardware failsafes. If the application closes or crashes, the engine executes a rapid cleanup sequence that instantly releases software overrides, safely returning all cooling channels back to native Motherboard BIOS control.
* **AI-Driven Stability Scoring & Resource Guardians:** Goes beyond simple error counting. The dashboard features a 24-hour rolling "Stability Score" algorithm that analyzes the severity, frequency, and time-decay of system events and hardware faults, translating raw telemetry into a dynamic, human-readable AI Summary. It includes an active background sentry that monitors RAM and Pagefile saturation, automatically throttling its own UI updates and dispatching localized warnings before a system out-of-memory crash can occur.
* **Comprehensive Security Dashboard:** Get an instant, accurate mirror of your native Windows Defender states. Actively monitor your Firewall, SmartScreen, BitLocker, Core Isolation, and Account Protection. Includes a custom interactive slider to instantly adjust UAC (User Account Control) consent behaviors, and one-click execution hooks to force malware signature updates or trigger Defender Quick Scans without ever opening the clunky Windows UI.
* **Neural DNS & Advanced Port Auditor:** Take absolute control over your web routing. Integrated directly into the Command Center, this module features a built-in network security scanner that maps all active TCP/UDP listening sockets to their parent processes, flagging wide-open 0.0.0.0 bindings. Includes a "Deep Hardening" execution hook that violently shuts down vulnerable Windows protocols (like SMBv1/v2/v3 and Port 445) at the registry and firewall level. Features server benchmarking, manual DNS configuration, and strict DoH (DNS over HTTPS) enforcement to prevent ISP snooping and ensure trace-free, encrypted browsing.
* **Automated Deep Remediation Engine:** Features an arsenal of specialized, one-click auto-fixes for complex Windows issues. Safely unlock and purge corrupted .NET UI caches (requiring Restart Manager integration), repair LUAFV virtualization, stage Secure Boot key enrollments, or instantly reset the Desktop Window Manager (DWM) stack to seamlessly recover from graphical exhaustion and screen flickering without rebooting.
* **Advanced System Cleaner:** A comprehensive, high-fidelity PC cleaner engine powered by an OTA-updatable Winapp2.ini database containing over 3,700 definitions. It features fully automated, silent background scheduling and an extensible post-clean orchestrator to automatically execute custom scripts or system maintenance tools (like DISM) immediately after a sweep. Engineered for power users, it supports loading custom databases, includes an intelligent "Conflict Inspector" that leverages the Windows Restart Manager to identify active file locks, and offers deep shell integration to verify target files before deletion. Wrapped in a modern, beautifully localized UI complete with real-time storage heatmaps, a 30-day historical analytics dashboard, and a strict safety-first logic layer that protects sensitive system categories by default.
* **Automated Background Autopilot:** A silent background engine that tracks system resources. Use custom sliders in the Maintenance menu to set specific memory usage percentages or time intervals (in hours) to trigger fully automated, background memory optimization without lifting a finger.
* **Secure Password Manager & Generator:** Includes a fully offline, AES-encrypted password manager backed by a secure local SQL database. Safely store, categorize, and manage credentials with one-click copy and reveal toggles. Features a standalone, advanced password generator accessible system-wide via custom global hotkeys.
* **Military-Grade File & Folder Encryptor:** Securely encrypt and decrypt any file, image, executable, or entire directory using AES-256-GCM encryption. Engineered with raw byte processing to prevent binary data corruption and on-the-fly background ZIP compression for folders. Heavy cryptographic tasks are safely offloaded to background threads, triggering a global UI lock and modern visual overlay to prevent interruption.
* **Custom Windows 11 ISO Builder:** This feature enables the creation of fully debloated, optimized installation media using a sophisticated offline servicing engine. It automates the generation of unattended setups to bypass strict Windows 11 hardware requirements (TPM, CPU, RAM), forces local account creation, and strips away over 50+ bloatware packages like Edge, OneDrive, Copilot and Cortana. By injecting hundreds of custom registry tweaks, removing legacy system components, and pre-tuning over 100 system services directly into the WIM/ESD image, it ensures a high-performance, privacy-focused Windows experience from the very first boot.
* **Integrated WinGet App Store:** A powerful, fully GUI-driven software center powered by the Windows Package Manager. Easily search, install, update, and uninstall thousands of applications without ever opening a terminal. Features an advanced context menu offering granular control for power users, including interactive visual setups, machine-wide deployments, silent administrative executions, and the ability to safely bypass hash integrity checks.
* **Advanced Startup & Boot Manager:** Take absolute control over your Windows boot sequence. Easily detect, toggle, or permanently delete auto-start applications from the registry and startup folders. Features an innovative "Cycle-Click" delay system to stagger application launches (15s–60s) preventing CPU "thundering herd" spikes during boot. Includes Authenticode digital signature verification to spot unverified publishers, and supports saving complex configurations into switchable Smart Profiles (Gaming/Work).
* **Active Startup Monitor:** A silent background sentinel that actively guards your registry and boot folders. It instantly triggers a system-wide alert if a new application stealthily adds itself to your startup sequence without your permission, giving you the immediate power to block it.
* **Group Policy Manager:** Includes a dedicated scanner to detect, review, and easily revert customized or corrupted Windows Group Policies back to default OS behavior.
* **Gaming Mode (Smart Sniper Optimization):** Instantly shifts your system into high gear with a single click. Intelligently suspends non-essential background tasks, over 80 Windows services, and scheduled tasks while strictly protecting a "Do Not Touch" whitelist of essential gaming companions (Steam, Discord, OBS, Anti-Cheats). Automatically unleashes GPU power states, unlocks CPU core affinity for games, and seamlessly restores your system to its exact original state when disabled.

---

### 🧠 Under the Hood: The Neural Analysis Engine (Diagnostics)
Traditional monitoring tools and task managers are passive—they overwhelm you with raw data, cluttered event logs, and leave you to figure out what is actually wrong. EvolveOS Optimizer flips this paradigm by acting as an active, intelligent sentry.
Here is how the underlying Neural Analysis Engine processes, protects, and repairs your system in real-time:

* **The Event Log Miner (Signal vs. Noise):** Windows generates thousands of routine telemetry logs every hour. The LiveEventWatcherHelper hooks directly into the OS event pipeline to silently intercept this data, aggressively filtering out normal system noise. It only bubbles up actionable intelligence—like unexpected application crashes, DWM exhaustion, or security bypasses. This data is fed into a 24-hour rolling "Stability Score" algorithm, which the AI Engine translates into a human-readable threat summary.
* **Predictive MemoryGuardian:** While other tools react to out-of-memory errors, the Neural Engine anticipates them. By querying high-performance WMI counters that perfectly mirror Task Manager, it tracks RAM and Pagefile saturation. If the app detects impending memory exhaustion, it triggers the MemoryGuardian to dynamically clamp the app's footprint down to 150MB (Efficiency Mode) and utilizes native Win32 APIs (EmptyWorkingSet) to force-reclaim unmanaged UI and DirectX memory before a system crash occurs.
* **Deep Security & Defender Sync:** The engine bypasses the clunky Windows Settings UI to query native Defender APIs directly. It builds an exact, real-time mirror of your system's defensive posture—tracking Firewall states, Core Isolation, BitLocker, LSA Protection, and Smart App Control. If a vulnerability is found, the engine utilizes contextual administrative elevation to instantly deploy automated fixes, allowing you to force signature updates or adjust UAC consent behaviors with a single click.
* **Active Hardware & Driver Interrogation:** Instead of just reading a static list of devices, the engine utilizes real-time WMI telemetry (Win32_DeviceChangeEvent) to monitor the Plug and Play (PnP) bus. If a driver stack crashes or a component fails, the integrated RemediationEngine intercepts the fault and can securely re-initialize the specific hardware driver stack to restore functionality without requiring a full system reboot.
* **Neural DNS & Port Auditor:** The engine actively audits your network routing for critical vulnerabilities. It performs high-speed socket mapping to detect dangerously exposed 0.0.0.0 bindings, tracing them back to their exact parent executables. It natively orchestrates DNSCrypt to enforce system-wide DNS-over-HTTPS (DoH) for total ISP blindness, and features a "Deep Hardening" sequence that aggressively shuts down legacy protocols (like SMBv1 and Port 445) at both the registry and firewall levels.
* **Automated Remediation Execution:** When the engine detects a critical fault, it doesn't just alert you—it offers a weaponized auto-fix. Whether it's unlocking and purging corrupted .NET UI caches using the Windows Restart Manager, repairing LUAFV virtualization, or seamlessly resetting a crashed Desktop Window Manager (DWM) stack, the engine deploys precise registry and service-level repairs to keep you running.

---

### 🧠 Under the Hood: The GamingModeHelper
Traditional "Game Boosters" are notorious for blindly killing processes, which often leads to crashed Discord calls, closed game launchers, or worse—triggered Anti-Cheat bans. EvolveOS Optimizer takes a different approach by acting as a precision sniper rather than a blunt instrument. 

Here is how the underlying `GamingModeHelper` safely pushes your system to its limits:

* **The Snapshot (State Capture):** Before applying any optimizations, the engine takes a comprehensive snapshot of your system's exact state. It reads your active Power Plan GUID, the startup types of over 80 individual Windows services, GPU registry values, and scheduled tasks. This is serialized into a secure JSON backup, guaranteeing a perfect recovery even if your PC crashes mid-game.
* **The "Do Not Touch" Whitelist:** Built directly into the code is a non-negotiable HashSet of protected processes. If an app matches this list (e.g., Vanguard, EasyAntiCheat, Steam, TeamSpeak, OBS, Logitech G HUB, Razer Synapse), the Optimizer strictly ignores it. Furthermore, it dynamically protects core Windows OS executables and essential Microsoft signed binaries from termination.
* **Deep System Tweaks:** Once bloat is safely cleared, the helper injects over 30 temporary registry tweaks. It reconfigures MMCSS (Multimedia Class Scheduler Service) to prioritize game rendering, adjusts Mouse/Keyboard queue sizes to reduce input latency, completely disables Network Throttling, and flushes ARP/DNS caches for the cleanest possible route to game servers.
* **GPU & CPU Unleashed:** The engine actively detects your hardware, forcing NVIDIA GPUs into "Prefer Maximum Performance" mode and disabling AMD's ULPS (Ultra-Low Power State). It then scans for active whitelisted games, automatically elevating their Process Priority to High and unlocking their Processor Affinity across all available CPU cores to prevent Windows parking.
* **The Graceful Restoration:** When Gaming Mode is toggled off, the engine reads the JSON backup file and meticulously reverses every single change. Suspended services are restarted, power plans are restored, and registry keys are set back to their exact previous values, leaving your PC exactly as you left it.

---

<!-- language --> 
<div align="center">
  <h1>🌍 Available Languages:</h1>

<a href="https://github.com/EvolveOS-Software/EvolveOS_Optimizer/blob/master/README.md">
    <img src="https://cdn-icons-png.flaticon.com/128/197/197484.png" alt="US Flag" width="40">
</a>

<a href="https://github.com/EvolveOS-Software/EvolveOS_Optimizer/blob/master/README.md">
    <img src="https://cdn-icons-png.flaticon.com/128/197/197614.png" alt="Dutch Flag" width="40">
</a>

<a href="https://github.com/EvolveOS-Software/EvolveOS_Optimizer/blob/master/README.md">
    <img src="https://cdn-icons-png.flaticon.com/128/197/197560.png" alt="French Flag" width="40">
</a>

<a href="https://github.com/EvolveOS-Software/EvolveOS-Optimizer/blob/master/README.md">
    <img src="https://cdn-icons-png.flaticon.com/128/197/197571.png" alt="German Flag" width="40">
</a>

<a href="https://github.com/EvolveOS-Software/EvolveOS-Optimizer/blob/master/README.md">
    <img src="https://cdn-icons-png.flaticon.com/128/9906/9906483.png" alt="Italian Flag" width="40">
</a>

</div>

---

## 📸 Screenshots

| Dashboard | Command Center | Command Center Tools |
|:---:|:---:|:---:|
| <img src="https://github.com/EvolveOS-Software/EvolveOS_Optimizer_V3.0/blob/master-net10.0/.github/Screenshots/HomePage.png" alt="EvolveOS Optimizer Dashboard" width="300"/> | <img src="https://github.com/EvolveOS-Software/EvolveOS_Optimizer_V3.0/blob/master-net10.0/.github/Screenshots/Diagnostics_1.png" alt="EvolveOS Command Center" width="300"/> | <img src="https://github.com/EvolveOS-Software/EvolveOS_Optimizer_V3.0/blob/master-net10.0/.github/Screenshots/Diagnostics_2.png" alt="EvolveOS Command Center Tools" width="300"/> |
| Process Manager | Startup Apps | System Cleaner |
|:---:|:---:|:---:|
 <img src="https://github.com/EvolveOS-Software/EvolveOS_Optimizer_V3.0/blob/master-net10.0/.github/Screenshots/ProcessManagerPage.png" alt="EvolveOS Process Manager" width="300"/> | <img src="https://github.com/EvolveOS-Software/EvolveOS_Optimizer_V3.0/blob/master-net10.0/.github/Screenshots/StartupManagerPage.png" alt="EvolveOS App Startup Manager" width="300"/> | <img src="https://github.com/EvolveOS-Software/EvolveOS_Optimizer_V3.0/blob/master-net10.0/.github/Screenshots/SystemCleanerPage.png" alt="EvolveOS System Cleaner" width="300"/> |
---

## 🛡️ Is EvolveOS Optimizer Safe? (Open Source & Transparent)
When searching for a PC system utility, safety and privacy should be the top priority. Because EvolveOS Optimizer is **100% open-source software**, the community can actively audit every line of code. We do not collect telemetry, there is no hidden adware, and there are no background tracking services. 

Furthermore, EvolveOS Optimizer is validated and available directly through Microsoft's official **WinGet** package manager, ensuring you are receiving a clean, trusted application.

---

## 🚀 Installation & Usage
EvolveOS Optimizer is a self-contained, single-file executable requiring no installation! It has successfully passed Microsoft's official SmartScreen and security validations and is proudly available via the Windows Package Manager (WinGet) or as a direct manual download. 

### Option 1: Download via WinGet (Recommended)
Open your terminal (Command Prompt or PowerShell) and run the following command:
```
winget install EvolveOS.Optimizer
```
*(Note: Once WinGet finishes downloading, open your Start Menu, search for **EvolveOS Optimizer**, and launch it to complete the initial setup.)*

---

### Option 2: Manual Download
1. Click the download button above to get the latest release, or browse the [Releases](../../releases) page for older versions.
2. Download the `EvolveOS_Optimizer.exe` file (or the `.zip` archive).
3. Place the executable in your preferred location (e.g., your Desktop or a dedicated `C:\Tools\` folder).
4. Run `EvolveOS_Optimizer.exe`.

---

### 🔄 Automatic Updates
You only need to download EvolveOS Optimizer once! Once the app is running on your system, its **built-in auto-updater** will automatically detect, download, and install all future versions.

💡 **Pro-Tip:** Enable **"Start with Windows"** and **"Hide to Tray"** in the app settings to let the Background Health Monitor protect your system continuously.

---

## 💖 Support the Project

EvolveOS Optimizer is far more than just a cleanup script—it is a meticulously engineered, open-source passion project. Countless hours of development have gone into bypassing WinUI 3 limitations, building a zero-footprint memory model, integrating deep ACL/Registry-level system tweaks, and perfecting the native OS background transitions.

If this tool has lowered your gaming ping and improved your gaming experience with faster framerates, protected your privacy, recovered gigabytes of wasted RAM and disk space, or simply provided you with a drastically faster and smoother overall Windows experience, please consider buying me a coffee! Your support is what keeps this project alive, 100% ad-free, and actively developed for the community.

[![Donate with PayPal](https://www.paypalobjects.com/en_US/i/btn/btn_donateCC_LG.gif)](https://www.paypal.com/donate/?hosted_button_id=UL8EXYHGKM3D2)

---
</br>

## 🌍 How to Contribute a Translation

Want to see EvolveOS Optimizer in your native language? We have built a dedicated set of Developer Tools right into the app to make translating incredibly fast and easy! You don't need to be a programmer to help out.

### Getting Started with the Built-In Translation Tools

**1. Enable Developer Mode**
Go to **Settings > Security & Privacy** and toggle **Developer Mode** to `ON`. A new "Developer Tools" section will appear at the bottom of the page.

**2. Starting your Translation**
* **Updating an existing language:** Simply change the app's language in the Appearance settings to the one you want to work on.
* **Adding a brand NEW language:** Scroll down to the Developer Tools menu, type your language code (e.g., `es-es` for Spanish) into the **Create New Language Template** box, and click Create. The app will automatically generate a new dictionary file filled with English defaults for you to translate!

**3. Turn on the Translation Hotkey**
Inside the Developer Tools menu, turn on the **Translation Debug Hotkey**. You can customize the shortcut, but the default is `Ctrl + Shift + L`.

**4. Browse the App & Spot Missing Strings**
As you navigate through the app, any text that hasn't been translated yet will automatically light up:
* 🟠 **Orange Text:** The string is missing in your language, so the app is using the English fallback.
* 🔴 **Red Text:** The string is completely missing from the app's dictionaries.

**5. Generate a Missing Strings Report**
Press your translation hotkey (`Ctrl + Shift + L`) on any page. A dialog will pop up showing you exactly which translation keys are missing on your current screen. 

Behind the scenes, the app automatically tracks every missing string you encounter and logs it into a JSON file (e.g., `MissingStrings_fr-fr.json`).

**6. Translate the JSON File**
* In the Developer Tools menu, click **Open JSON File**. *(Note: If Windows doesn't know how to open JSON files, it will open File Explorer. Just right-click the file and open it with Notepad, VS Code, or your favorite text editor).*
* You will see a list of missing keys alongside their English defaults. Simply change the English text to your language, save, and close the file.

**7. Merge and See Your Changes!**
* Go back to the Developer Tools menu in the app and click **Merge to XAML**.
* The app will automatically inject your newly translated JSON strings directly into your language's `.xaml` dictionary file and refresh the UI. The orange/red text will instantly turn back to normal!

**8. Submit your Translation**
Once you are happy with your translations, it's time to share them! 
* Click the **Locate Language File** button in the Developer Tools. This will instantly open File Explorer and highlight your finished `.xaml` file. 
* Simply upload that file to your fork and submit a Pull Request. Thank you for your contribution! ❤️

</br>

## 🛠️ Building from Source

To build this project locally, you will need the following development environment:

* **Visual Studio 2026** (Version 18.0 or higher)
* **.NET 10.0 SDK** (or higher)
* **Windows App SDK** component (installed via the Visual Studio Installer)

```bash
# 1. Clone the repository
git clone https://github.com/EvolveOS-Software/EvolveOS_Optimizer_V3.0.git

# 2. Open the solution (.sln) in Visual Studio
# 3. Restore NuGet packages
# 4. Build and Run!
```

## 📦 Publishing a Standalone Release (Self-Contained)

* ** If you are deploying this application to a fresh Windows installation, it is highly recommended to publish it as a Self-Contained application.This bundles the .NET Desktop Runtime directly into the app folder so the user is not prompted to download any dependencies.
```bash
# 1. Right-click the EvolveOS_Optimizer project in the Solution Explorer and select Publish.
# 2. Select the target Folder.
# 3. Click on Show all settings (or the pencil icon) in the publish profile and ensure the following are set:
     - Deployment Mode: Self-Contained
     - Target Runtime: win-x64
# 4. Click Publish.
```

---

## 📬 Contact

<img src="https://avatars.githubusercontent.com/u/203890833?s=400&u=94c1b9e1e32396b0112ae765f9cc87a71dee0a64&v=4" width="100px;" style="border-radius: 50%;"/>

<p align="left">
  <a href="https://github.com/EvolveOS-Software">
    <img src="https://img.shields.io/badge/Github-gray?style=for-the-badge&logo=github&logoColor=white" />
  </a>
  <a href="mailto:evolveossoftware@gmail.com">
    <img src="https://img.shields.io/badge/Gmail-D14836?style=for-the-badge&logo=gmail&logoColor=white" alt="Gmail" />
  </a>
</p>