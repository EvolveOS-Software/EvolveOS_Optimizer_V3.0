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
* **3-Tier Context-Aware Notification Engine: A custom-built, queue-based notification manager that intelligently routes alerts based on your exact window state. If the dashboard is active, it delivers beautifully animated, severity-colored in-app banners. If the app is minimized, it triggers a custom, non-intrusive overlay notification. If running silently in the system tray, it seamlessly delegates to Windows Native Adaptive Toasts to keep you informed without interrupting your workflow.
* **Secure Password Manager & Generator: Includes a fully offline, AES-encrypted password manager backed by a secure local SQL database. Safely store, categorize, and manage credentials with one-click copy and reveal toggles. Features a standalone, advanced password generator accessible system-wide via custom global hotkeys.
* **Advanced User Authentication: Features a robust, multi-user accounts management system powered by an auto-installing SQL LocalDB backend. Keep your application access and encrypted vaults entirely private and completely disconnected from the cloud.
* **Dynamic Hardware Dashboard: A beautifully animated dashboard featuring real-time history line graphs for CPU, RAM, GPU, and dual-line Network speeds. Fully interactive with smooth scale animations on hover and a customizable global timeframe setting.
* **Built-in Process Manager:** Includes a dedicated, real-time process manager to monitor memory usage (MB), thread counts, and PIDs. Easily search, sort, and forcefully terminate resource-heavy applications directly within the app.
* **Security Center Dashboard: Provides a comprehensive overview of system security, actively monitoring Firewall, SmartScreen, BitLocker, Core Isolation, Account Protection, and UAC levels. Accurately mirrors native Windows Defender states and includes a custom slider to instantly adjust UAC consent behaviors.
* **One-Click Defender Actions:** Bypass the standard Windows UI to instantly trigger Windows Defender Quick Scans or force malware signature updates directly via PowerShell integration.
* **Custom Script Engine:** A dedicated dynamic scripts hub allows users to load, refresh, and execute custom scripts individually or in bulk via a multi-select mode.
* **Advanced Memory & Disk Management:** Intelligently flushes the Working Set, System File Cache, Modified Page Lists, and safely clears system caches, DNS, and Windows Update leftovers.
* **Ultimate Privacy Shield:** Deep registry tweaks to completely disable Windows telemetry, diagnostic data collection, targeted advertising, and intrusive AI features like Copilot and Recall.
* **Bloatware Decimation:** Cleanly force-uninstall pre-packaged UWP apps, including deeply embedded software like Microsoft Edge, OneDrive, Cortana, and third-party sponsored bloatware.
* **Network & DNS Optimizer:** Built-in DNS changer that automatically pings and finds the fastest servers. Features deep DNSCrypt integration to fully encrypt your internet routing, prevent ISP snooping, and enforce strict zero-log policies for completely invisible, trace-free web browsing.
* **Advanced Service & Defender Control:** Safely suspend unnecessary background services (Xbox, Hyper-V, Maps) to free up resources. Features a powerful, ACL-level bypass to completely disable or restore Windows Defender via NSudo.
* **System & UI Customization:** Fine-tune Windows Explorer, restore classic context menus, adjust keyboard/mouse input delays, and strip away resource-heavy visual effects.
* **Group Policy Manager:** Includes a dedicated scanner to detect, review, and easily revert customized or corrupted Windows Group Policies back to default OS behavior.
* **Gaming Performance:** Instantly import custom power plans, disable Game DVR/Bar, and remove network throttling to ensure lower latency and higher frame rates.
* **Automated Background Monitoring:** A silent background engine that tracks RAM and Disk usage, alerting you only when critical thresholds are reached.
* **System Tray Integration: Run silently in the background with an ultra-light footprint, featuring a quick-access context menu to instantly jump to key pages or trigger optimizations directly from the taskbar without opening the full UI.
* **Portable Execution:** Runs as a standalone, unpacked single executable. No bulky installers or registry bloat.

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

### 📝 How to Translate a Program
To translate the program into your language, download the file [en-us.xaml (EN)](https://github.com/EvolveOS-Software/EvolveOS_Optimizer_V3.0/blob/master-net10.0/EvolveOS_Optimizer/Languages/en-us.xaml). Translate it and place it in the language folder named according to your language code. Then, submit a **Pull Request**.

Or choose a more suitable language for you at the path:
<div>
    <pre>
📂 .Source
└── 📁 EvolveOS_Optimizer
    └── 📁 Languages
    </pre>
</div>

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

---

## Contact
<img src="https://avatars.githubusercontent.com/u/203890833?s=400&u=94c1b9e1e32396b0112ae765f9cc87a71dee0a64&v=4" width="100px;"/>

[![github](https://img.shields.io/badge/Github-gray?style=for-the-badge&logo=github&logoColor=white)](https://github.com/EvolveOS-Software)

