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

**A modern, lightweight, and aggressive system optimization tool built with WinUI 3 and the Windows App SDK.**

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D7.svg?style=flat-square&logo=windows)](https://github.com/EvolveOS-Software/EvolveOS_Optimizer_V3.0)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](https://opensource.org/licenses/MIT)
[![WinUI 3](https://img.shields.io/badge/UI-WinUI%203-blueviolet?style=flat-square)](#)

</div>

---

## 📖 About The Project

The inspiration for this project came from navigating the current landscape of Windows optimizers and debloat utilities. While many of these tools are useful, I wanted to build an application that perfectly aligned with my own vision of an optimization hub. The result is a tool designed from the ground up to offer a refined interface, fluid usability, and powerful under-the-hood tweaks that significantly elevate the Windows experience.

EvolveOS Optimizer is a premium, open-source system maintenance utility designed to keep your Windows environment running at peak performance. Built on the modern **WinUI 3** framework, it features a deeply integrated background health monitor, native OS notifications, and a highly customizable, gorgeous UI. 

Experience a dynamic dashboard where you can effortlessly drag and drop cards like System Health, System Security, and DNS Encryption to fit your workflow. The interface is brought to life with smooth Fluent animations, full Light and Dark theme support, and advanced window backdrops (Mica, Mica Alt, Acrylic, and Acrylic Thin) complete with precise color and translucency sliders.

Whether running actively on your dashboard or silently in your system tray, EvolveOS Optimizer continuously protects your system from memory leaks, cache bloat, and resource exhaustion without interrupting your workflow.

## ✨ Key Features

* **Zero-Footprint Architecture:** As a system optimizer, the app itself is engineered to consume as little memory as possible. It utilizes a strict zero-cache navigation model where every page implements a custom `IPurgeable` interface. ViewModels are aggressively disposed, background threads are cancelled, and UI elements are completely purged from memory the moment you navigate away—ensuring perfectly fluent browsing with zero passive memory bloat.
* **Global Optimization Hotkeys:** Configure custom keyboard shortcuts to instantly flush system memory, clear DNS cache, and wipe temp files at any time. Triggering this directly optimizes the app itself—drastically reducing active memory usage when the dashboard is open, and aggressively trimming its background footprint down to an ultra-light 35-50 MB when hidden in the system tray.
* **Automated Background Autopilot:** A silent background engine that tracks system resources. Use custom sliders in the Maintenance menu to set specific memory usage percentages or time intervals (in hours) to trigger fully automated, background memory optimization without lifting a finger.
* **3-Tier Context-Aware Notification Engine:** A custom-built, queue-based notification manager that intelligently routes alerts based on your exact window state. If the dashboard is active, it delivers beautifully animated, severity-colored in-app banners. If the app is minimized, it triggers a custom, non-intrusive overlay notification. If running silently in the system tray, it seamlessly delegates to Windows Native Adaptive Toasts to keep you informed without interrupting your workflow.
* **Secure Password Manager & Generator:** Includes a fully offline, AES-encrypted password manager backed by a secure local SQL database. Safely store, categorize, and manage credentials with one-click copy and reveal toggles. Features a standalone, advanced password generator accessible system-wide via custom global hotkeys.
* **Military-Grade File & Folder Encryptor:** Securely encrypt and decrypt any file, image, executable, or entire directory using AES-256-GCM encryption. Engineered with raw byte processing to prevent binary data corruption and on-the-fly background ZIP compression for folders. Heavy cryptographic tasks are safely offloaded to background threads, triggering a global UI lock and modern visual overlay to prevent interruption.
* **Custom Windows 11 ISO Builder:** This feature enables the creation of fully debloated, optimized installation media using a sophisticated offline servicing engine. It automates the generation of unattended setups to bypass strict Windows 11 hardware requirements (TPM, CPU, RAM), forces local account creation, and strips away over 50+ bloatware packages like Edge, OneDrive, Copilot and Cortana. By injecting hundreds of custom registry tweaks, removing legacy system components, and pre-tuning over 100 system services directly into the WIM/ESD image, it ensures a high-performance, privacy-focused Windows experience from the very first boot.
* **Advanced User Authentication:** Features a robust, multi-user accounts management system powered by an auto-installing SQL LocalDB backend. Keep your application access and encrypted vaults entirely private and completely disconnected from the cloud.
* **Dynamic Hardware Dashboard:** A beautifully animated dashboard featuring real-time history line graphs for CPU, RAM, GPU, and dual-line Network speeds. Fully interactive with smooth scale animations on hover and a customizable global timeframe setting.
* **Built-in Process Manager:** Includes a dedicated, real-time process manager to monitor memory usage (MB), thread counts, and PIDs. Easily search, sort, and forcefully terminate resource-heavy applications directly within the app.
* **Security Center Dashboard:** Provides a comprehensive overview of system security, actively monitoring Firewall, SmartScreen, BitLocker, Core Isolation, Account Protection, and UAC levels. Accurately mirrors native Windows Defender states and includes a custom slider to instantly adjust UAC consent behaviors.
* **One-Click Defender Actions:** Bypass the standard Windows UI to instantly trigger Windows Defender Quick Scans or force malware signature updates directly via PowerShell integration.
* **Custom Script Engine:** A dedicated dynamic scripts hub allows users to load, refresh, and execute custom scripts individually or in bulk via a multi-select mode.
* **Advanced Memory & Disk Management:** Intelligently flushes the Working Set, System File Cache, Modified Page Lists, and safely clears system caches, DNS, and Windows Update leftovers.
* **Ultimate Privacy Shield:** Deep registry tweaks to completely disable Windows telemetry, diagnostic data collection, targeted advertising, and intrusive AI features like Copilot and Recall.
* **Bloatware Decimation:** Cleanly force-uninstall pre-packaged UWP apps, including deeply embedded software like Microsoft Edge, OneDrive, Cortana, and third-party sponsored bloatware.
* **Network & DNS Optimizer:** Built-in DNS changer that automatically pings and finds the fastest servers. Features deep DNSCrypt integration to fully encrypt your internet routing, prevent ISP snooping, and enforce strict zero-log policies for completely invisible, trace-free web browsing.
* **Advanced Service & Defender Control:** Safely suspend unnecessary background services (Xbox, Hyper-V, Maps) to free up resources. Features a powerful, ACL-level bypass to completely disable or restore Windows Defender via NSudo.
* **System & UI Customization:** Fine-tune Windows Explorer, restore classic context menus, adjust keyboard/mouse input delays, and strip away resource-heavy visual effects.
* **Group Policy Manager:** Includes a dedicated scanner to detect, review, and easily revert customized or corrupted Windows Group Policies back to default OS behavior.
* **Gaming Mode (Smart Sniper Optimization): Instantly shifts your system into high gear with a single click. Intelligently suspends non-essential background tasks, over 80 Windows services, and scheduled tasks while strictly protecting a "Do Not Touch" whitelist of essential gaming companions (Steam, Discord, OBS, Anti-Cheats). Automatically unleashes GPU power states, unlocks CPU core affinity for games, and seamlessly restores your system to its exact original state when disabled.
* **Automated Background Monitoring:** A silent background engine that tracks RAM and Disk usage, alerting you only when critical thresholds are reached.
* **System Tray Integration:** Run silently in the background with an ultra-light footprint, featuring a quick-access context menu to instantly jump to key pages or trigger optimizations directly from the taskbar without opening the full UI.
* **Portable Execution:** Runs as a standalone, unpacked single executable. No bulky installers or registry bloat.

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

| Dashboard | Maintenance | Security |
|:---:|:---:|:---:|
| <img src="https://github.com/EvolveOS-Software/EvolveOS_Optimizer_V3.0/blob/master-net10.0/.github/Screenshots/HomePage.png" alt="EvolveOS Optimizer Dashboard" width="300"/> | <img src="https://github.com/EvolveOS-Software/EvolveOS_Optimizer_V3.0/blob/master-net10.0/.github/Screenshots/MaintenancePage.png" alt="EvolveOS Security Maintenance" width="300"/> | <img src="https://github.com/EvolveOS-Software/EvolveOS_Optimizer_V3.0/blob/master-net10.0/.github/Screenshots/SecurityPage.png" alt="EvolveOS Security Center" width="300"/> |
| Dns Changer | Settings | Other |
|:---:|:---:|:---:|
 <img src="https://github.com/EvolveOS-Software/EvolveOS_Optimizer_V3.0/blob/master-net10.0/.github/Screenshots/DnsPage.png" alt="EvolveOS Optimizer Dashboard" width="300"/> | <img src="https://github.com/EvolveOS-Software/EvolveOS_Optimizer_V3.0/blob/master-net10.0/.github/Screenshots/SettingsPage.png" alt="EvolveOS Security Maintenance" width="300"/> | <img src="https://github.com/EvolveOS-Software/EvolveOS_Optimizer_V3.0/blob/master-net10.0/.github/Screenshots/SettingsPage.png" alt="EvolveOS Security Center" width="300"/> |
---

## 🚀 Installation & Usage

EvolveOS Optimizer is distributed as a portable, single-file executable. No installation is required!

1. Click the download button above to get the latest release, or browse the [Releases](../../releases) page for older versions.
2. Download the `EvolveOS_Optimizer.exe` file (or the `.zip` archive).
3. Place the executable in your preferred location (e.g., your Desktop or a dedicated `C:\Tools\` folder).
4. Run `EvolveOS_Optimizer.exe`.
5. **Pro-Tip:** Enable *"Start with Windows"* and *"Hide to Tray"* in the app settings to let the Background Health Monitor protect your system continuously.

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

