// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Utilities.WinBuilder
{
    public static class EvolveOSCatalog
    {
        #region Removable Apps

        public static List<RemovableApp> GetAvailableApps()
        {
            return new List<RemovableApp>
            {
                // Core Windows Apps
                new RemovableApp { DisplayName = "3D Viewer", PackageName = "Microsoft.Microsoft3DViewer", IconPath = "ms-appx:///Assets/ImagePackages/Microsoft3DViewer.png", Description = ResourceString.GetString("AppDesc_3DViewer") ?? "View, create, and print 3D models." },
                new RemovableApp { DisplayName = "Alarms & Clock", PackageName = "Microsoft.WindowsAlarms", IconPath = "ms-appx:///Assets/ImagePackages/Alarms.png", Description = ResourceString.GetString("AppDesc_Alarms") ?? "Manage alarms, timers, and stopwatches." },
                new RemovableApp { DisplayName = "Calculator", PackageName = "Microsoft.WindowsCalculator", IconPath = "ms-appx:///Assets/ImagePackages/Calculator.png", Description = ResourceString.GetString("AppDesc_Calculator") ?? "Standard, scientific, and programmer calculator." },
                new RemovableApp { DisplayName = "Camera", PackageName = "Microsoft.WindowsCamera", IconPath = "ms-appx:///Assets/ImagePackages/Camera.png", Description = ResourceString.GetString("AppDesc_Camera") ?? "Take photos and videos with your webcam." },
                new RemovableApp { DisplayName = "Clipchamp", PackageName = "Clipchamp.Clipchamp", IconPath = "ms-appx:///Assets/ImagePackages/Clipchamp.png", Description = ResourceString.GetString("AppDesc_Clipchamp") ?? "Microsoft's cloud-based video editor." },
                new RemovableApp { DisplayName = "Copilot", PackageName = "Copilot", IconPath = "ms-appx:///Assets/ImagePackages/Copilot.png", Description = ResourceString.GetString("AppDesc_Copilot") ?? "AI assistant integrated directly into Windows." },
                new RemovableApp { DisplayName = "Cortana", PackageName = "Microsoft.549981C3F5F10", IconPath = "ms-appx:///Assets/ImagePackages/Cortana.png", Description = ResourceString.GetString("AppDesc_Cortana") ?? "Deprecated legacy digital assistant." },
                new RemovableApp { DisplayName = "Dev Home", PackageName = "Microsoft.Windows.DevHome", IconPath = "ms-appx:///Assets/ImagePackages/DevHome.png", Description = ResourceString.GetString("AppDesc_DevHome") ?? "Development environment dashboard and hub." },
                new RemovableApp { DisplayName = "Feedback Hub", PackageName = "Microsoft.WindowsFeedbackHub", IconPath = "ms-appx:///Assets/ImagePackages/FeedbackHub.png", Description = ResourceString.GetString("AppDesc_FeedbackHub") ?? "Submit bug reports and telemetry to Microsoft." },
                new RemovableApp { DisplayName = "Get Help", PackageName = "Microsoft.GetHelp", IconPath = "ms-appx:///Assets/ImagePackages/GetHelp.png", Description = ResourceString.GetString("AppDesc_GetHelp") ?? "Windows troubleshooting and virtual support." },
                new RemovableApp { DisplayName = "Get Started", PackageName = "Microsoft.Getstarted", IconPath = "ms-appx:///Assets/ImagePackages/GetStarted.png", Description = ResourceString.GetString("AppDesc_GetStarted") ?? "Tips and tricks for new Windows users." },
                new RemovableApp { DisplayName = "Mail & Calendar", PackageName = "microsoft.windowscommunicationsapps", IconPath = "ms-appx:///Assets/ImagePackages/Mail.png", Description = ResourceString.GetString("AppDesc_Mail") ?? "Default email and scheduling client." },
                new RemovableApp { DisplayName = "Maps", PackageName = "Microsoft.WindowsMaps", IconPath = "ms-appx:///Assets/ImagePackages/Maps.png", Description = ResourceString.GetString("AppDesc_Maps") ?? "Navigation and location services." },
                new RemovableApp { DisplayName = "Microsoft Family", PackageName = "MicrosoftCorporationII.MicrosoftFamily", IconPath = "ms-appx:///Assets/ImagePackages/MicrosoftFamilySafety.png", Description = ResourceString.GetString("AppDesc_Family") ?? "Parental controls and screen time tracking." },
                new RemovableApp { DisplayName = "Microsoft Store", PackageName = "Microsoft.WindowsStore", IconPath = "ms-appx:///Assets/ImagePackages/MicrosoftStore.png", Description = ResourceString.GetString("AppDesc_Store") ?? "Official app storefront for Windows." },
                new RemovableApp { DisplayName = "Mixed Reality Portal", PackageName = "Microsoft.MixedReality.Portal", IconPath = "ms-appx:///Assets/ImagePackages/MixedReality.png", Description = ResourceString.GetString("AppDesc_MixedReality") ?? "VR/AR headset environment setup." },
                new RemovableApp { DisplayName = "Notepad", PackageName = "Microsoft.WindowsNotepad", IconPath = "ms-appx:///Assets/ImagePackages/Notepad.png", Description = ResourceString.GetString("AppDesc_Notepad") ?? "Default plain text editor." },
                new RemovableApp { DisplayName = "Office Hub", PackageName = "Microsoft.MicrosoftOfficeHub", IconPath = "ms-appx:///Assets/ImagePackages/MicrosoftOfficeHub.png", Description = ResourceString.GetString("AppDesc_OfficeHub") ?? "Launcher for Microsoft 365 applications." },
                new RemovableApp { DisplayName = "OneNote", PackageName = "Microsoft.Office.OneNote", IconPath = "ms-appx:///Assets/ImagePackages/OneNote.png", Description = ResourceString.GetString("AppDesc_OneNote") ?? "Digital note-taking application." },
                new RemovableApp { DisplayName = "Outlook for Windows", PackageName = "Microsoft.OutlookForWindows", IconPath = "ms-appx:///Assets/ImagePackages/Outlook.png", Description = ResourceString.GetString("AppDesc_Outlook") ?? "New web-based email client replacement." },
                new RemovableApp { DisplayName = "Paint", PackageName = "Microsoft.Paint", IconPath = "ms-appx:///Assets/ImagePackages/Paint.png", Description = ResourceString.GetString("AppDesc_Paint") ?? "Basic image editing and drawing app." },
                new RemovableApp { DisplayName = "People", PackageName = "Microsoft.People", IconPath = "ms-appx:///Assets/ImagePackages/People.png", Description = ResourceString.GetString("AppDesc_People") ?? "Contact management address book." },
                new RemovableApp { DisplayName = "Photos", PackageName = "Microsoft.Windows.Photos", IconPath = "ms-appx:///Assets/ImagePackages/Photos.png", Description = ResourceString.GetString("AppDesc_Photos") ?? "Image viewer and basic media editor." },
                new RemovableApp { DisplayName = "Power Automate", PackageName = "Microsoft.PowerAutomateDesktop", IconPath = "ms-appx:///Assets/ImagePackages/MicrosoftPowerAutomate.png", Description = ResourceString.GetString("AppDesc_PowerAutomate") ?? "Desktop task and workflow automation." },
                new RemovableApp { DisplayName = "Quick Assist", PackageName = "MicrosoftCorporationII.QuickAssist", IconPath = "ms-appx:///Assets/ImagePackages/QuickAssist.png", Description = ResourceString.GetString("AppDesc_QuickAssist") ?? "Remote desktop tech support tool." },
                new RemovableApp { DisplayName = "Skype", PackageName = "Microsoft.SkypeApp", IconPath = "ms-appx:///Assets/ImagePackages/SkypeApp.png", Description = ResourceString.GetString("AppDesc_Skype") ?? "Video chat and voice calling app." },
                new RemovableApp { DisplayName = "Snipping Tool", PackageName = "Microsoft.ScreenSketch", IconPath = "ms-appx:///Assets/ImagePackages/ScreenSketch.png", Description = ResourceString.GetString("AppDesc_SnippingTool") ?? "Capture screenshots and screen recordings." },
                new RemovableApp { DisplayName = "Solitaire Collection", PackageName = "Microsoft.MicrosoftSolitaireCollection", IconPath = "ms-appx:///Assets/ImagePackages/MicrosoftSolitaireCollection.png", Description = ResourceString.GetString("AppDesc_Solitaire") ?? "Classic card games collection." },
                new RemovableApp { DisplayName = "Sound Recorder", PackageName = "Microsoft.WindowsSoundRecorder", IconPath = "ms-appx:///Assets/ImagePackages/SoundRecorder.png", Description = ResourceString.GetString("AppDesc_SoundRecorder") ?? "Record audio from your microphone." },
                new RemovableApp { DisplayName = "Sticky Notes", PackageName = "Microsoft.MicrosoftStickyNotes", IconPath = "ms-appx:///Assets/ImagePackages/MicrosoftStickyNotes.png", Description = ResourceString.GetString("AppDesc_StickyNotes") ?? "Digital post-it notes for your desktop." },
                new RemovableApp { DisplayName = "Teams", PackageName = "MSTeams", IconPath = "ms-appx:///Assets/ImagePackages/MicrosoftTeams.png", Description = ResourceString.GetString("AppDesc_Teams") ?? "Chat, meeting, and collaboration app." },
                new RemovableApp { DisplayName = "To Do", PackageName = "Microsoft.Todos", IconPath = "ms-appx:///Assets/ImagePackages/Todos.png", Description = ResourceString.GetString("AppDesc_ToDo") ?? "Task management and checklists." },
                new RemovableApp { DisplayName = "Xbox App (Legacy)", PackageName = "Microsoft.XboxApp", IconPath = "ms-appx:///Assets/ImagePackages/Xbox.png", Description = ResourceString.GetString("AppDesc_XboxApp") ?? "Legacy Xbox companion app." },
                new RemovableApp { DisplayName = "Xbox App (Modern)", PackageName = "Microsoft.GamingApp", IconPath = "ms-appx:///Assets/ImagePackages/Xbox.png", Description = ResourceString.GetString("AppDesc_XboxAppModern") ?? "Modern PC Game Pass and Xbox hub." },
                new RemovableApp { DisplayName = "Xbox Game Bar", PackageName = "Microsoft.XboxGamingOverlay", IconPath = "ms-appx:///Assets/ImagePackages/Xbox.png", Description = ResourceString.GetString("AppDesc_XboxGameBar") ?? "In-game overlay for capturing and performance." },
                new RemovableApp { DisplayName = "Xbox Game Overlay", PackageName = "Microsoft.XboxGameOverlay", IconPath = "ms-appx:///Assets/ImagePackages/Xbox.png", Description = ResourceString.GetString("AppDesc_XboxGameOverlay") ?? "Background gaming overlay service." },
                new RemovableApp { DisplayName = "Xbox Identity Provider", PackageName = "Microsoft.XboxIdentityProvider", IconPath = "ms-appx:///Assets/ImagePackages/Xbox.png", Description = ResourceString.GetString("AppDesc_XboxIdentity") ?? "Xbox live login authentication service." },
                new RemovableApp { DisplayName = "Xbox TCUI", PackageName = "Microsoft.Xbox.TCUI", IconPath = "ms-appx:///Assets/ImagePackages/Xbox.png", Description = ResourceString.GetString("AppDesc_XboxTCUI") ?? "Xbox Live in-game UI framework." },
                new RemovableApp { DisplayName = "Xbox Speech", PackageName = "Microsoft.XboxSpeechToTextOverlay", IconPath = "ms-appx:///Assets/ImagePackages/Xbox.png", Description = ResourceString.GetString("AppDesc_XboxSpeech") ?? "In-game speech-to-text transcription." },
                new RemovableApp { DisplayName = "Phone Link", PackageName = "Microsoft.YourPhone", IconPath = "ms-appx:///Assets/ImagePackages/Phone.png", Description = ResourceString.GetString("AppDesc_PhoneLink") ?? "Sync texts and calls with your smartphone." },
                new RemovableApp { DisplayName = "Zune Music", PackageName = "Microsoft.ZuneMusic", IconPath = "ms-appx:///Assets/ImagePackages/Music.png", Description = ResourceString.GetString("AppDesc_ZuneMusic") ?? "Legacy groove music player." },
                new RemovableApp { DisplayName = "Zune Video", PackageName = "Microsoft.ZuneVideo", IconPath = "ms-appx:///Assets/ImagePackages/Video.png", Description = ResourceString.GetString("AppDesc_ZuneVideo") ?? "Legacy movies & TV player." },

                // Third-Party Sponsor Bloatware
                new RemovableApp { DisplayName = "Bing News", PackageName = "BingNews", IconPath = "ms-appx:///Assets/ImagePackages/BingNews.png", Description = ResourceString.GetString("AppDesc_BingNews") ?? "Sponsored news app." },
                new RemovableApp { DisplayName = "Bing Search", PackageName = "BingSearch", IconPath = "ms-appx:///Assets/ImagePackages/BingSearch.png", Description = ResourceString.GetString("AppDesc_BingSearch") ?? "Sponsored search integration." },
                new RemovableApp { DisplayName = "Bing Weather", PackageName = "BingWeather", IconPath = "ms-appx:///Assets/ImagePackages/BingWeather.png", Description = ResourceString.GetString("AppDesc_BingWeather") ?? "Sponsored weather app." },
                new RemovableApp { DisplayName = "Disney+", PackageName = "Disney", IconPath = "ms-appx:///Assets/ImagePackages/Disney.png", Description = ResourceString.GetString("AppDesc_Disney") ?? "Sponsored streaming shortcut." },
                new RemovableApp { DisplayName = "Facebook", PackageName = "Facebook", IconPath = "ms-appx:///Assets/ImagePackages/Facebook.png", Description = ResourceString.GetString("AppDesc_Facebook") ?? "Sponsored social media app." },
                new RemovableApp { DisplayName = "Instagram", PackageName = "Instagram", IconPath = "ms-appx:///Assets/ImagePackages/Instagram.png", Description = ResourceString.GetString("AppDesc_Instagram") ?? "Sponsored social media app." },
                new RemovableApp { DisplayName = "LinkedIn (Legacy)", PackageName = "LinkedInforWindows", IconPath = "ms-appx:///Assets/ImagePackages/Linkedin.png", Description = ResourceString.GetString("AppDesc_LinkedInLegacy") ?? "Sponsored professional network app." },
                new RemovableApp { DisplayName = "LinkedIn (Modern)", PackageName = "7EE7776C.LinkedInforWindows", IconPath = "ms-appx:///Assets/ImagePackages/Linkedin.png", Description = ResourceString.GetString("AppDesc_LinkedInModern") ?? "Sponsored professional network app." },
                new RemovableApp { DisplayName = "Netflix", PackageName = "Netflix", IconPath = "ms-appx:///Assets/ImagePackages/Netflix.png", Description = ResourceString.GetString("AppDesc_Netflix") ?? "Sponsored streaming shortcut." },
                new RemovableApp { DisplayName = "Prime Video", PackageName = "PrimeVideo", IconPath = "ms-appx:///Assets/ImagePackages/PrimeVideo.png", Description = ResourceString.GetString("AppDesc_PrimeVideo") ?? "Sponsored streaming shortcut." },
                new RemovableApp { DisplayName = "Spotify", PackageName = "Spotify", IconPath = "ms-appx:///Assets/ImagePackages/Spotify.png", Description = ResourceString.GetString("AppDesc_Spotify") ?? "Sponsored music streaming app." },
                new RemovableApp { DisplayName = "TikTok", PackageName = "TikTok", IconPath = "ms-appx:///Assets/ImagePackages/TikTok.png", Description = ResourceString.GetString("AppDesc_TikTok") ?? "Sponsored social media app." },
                new RemovableApp { DisplayName = "WhatsApp", PackageName = "WhatsApp", IconPath = "ms-appx:///Assets/ImagePackages/WhatsApp.png", Description = ResourceString.GetString("AppDesc_WhatsApp") ?? "Sponsored messaging app." }
            };
        }

        #endregion

        #region Removable Elements

        public static List<RemovableElement> GetAvailableElements()
        {
            return new List<RemovableElement>
            {
                // Optional Features
                new RemovableElement { DisplayName = "Legacy Windows Media Player", PackageName = "MediaPlayback", Description = "The ancient Windows Media Player from the Windows 7 era.", IsCapability = false, IconPath = "ms-appx:///Assets/ImagePackages/Video.png" },
                new RemovableElement { DisplayName = "Microsoft XPS Document Writer", PackageName = "Printing-XPSServices-Features", Description = "Legacy XPS virtual printer.", IsCapability = false, IconPath = null },
                new RemovableElement { DisplayName = "Work Folders Client", PackageName = "WorkFolders-Client", Description = "Enterprise sync for corporate files.", IsCapability = false, IconPath = null },
                new RemovableElement { DisplayName = "Windows TIFF IFilter", PackageName = "TiffFilter", Description = "Legacy OCR for ancient TIFF image formats.", IsCapability = false, IconPath = null },
                
                // Capabilities / Features on Demand
                new RemovableElement { DisplayName = "Internet Explorer 11", PackageName = "Browser.InternetExplorer~~~~0.0.11.0", Description = "The dead IE11 browser engine.", IsCapability = true, IconPath = "ms-appx:///Assets/ImagePackages/Edge.png" },
                new RemovableElement { DisplayName = "WordPad", PackageName = "Microsoft.Windows.WordPad~~~~0.0.1.0", Description = "Legacy basic text editor.", IsCapability = true, IconPath = "ms-appx:///Assets/ImagePackages/Notepad.png" },
                new RemovableElement { DisplayName = "Steps Recorder", PackageName = "App.StepsRecorder~~~~0.0.1.0", Description = "Legacy troubleshooting tool.", IsCapability = true, IconPath = null },
                new RemovableElement { DisplayName = "Quick Assist", PackageName = "App.Support.QuickAssist~~~~0.0.1.0", Description = "Built-in remote desktop assistance.", IsCapability = true, IconPath = "ms-appx:///Assets/ImagePackages/QuickAssist.png" },
                new RemovableElement { DisplayName = "Math Recognizer", PackageName = "MathRecognizer~~~~0.0.1.0", Description = "Handwriting engine for math equations.", IsCapability = true, IconPath = "ms-appx:///Assets/ImagePackages/Calculator.png" },
                new RemovableElement { DisplayName = "PowerShell ISE", PackageName = "Microsoft.Windows.PowerShell.ISE~~~~0.0.1.0", Description = "Legacy graphical editor for PowerShell.", IsCapability = true, IconPath = "ms-appx:///Assets/ImageScriptsFiles/PowershellFile.png" },
                new RemovableElement { DisplayName = "Windows Mixed Reality", PackageName = "Analog.Holographic.Desktop~~~~0.0.1.0", Description = "Removes the Windows Mixed Reality portal.", IsCapability = true, IconPath = "ms-appx:///Assets/ImagePackages/MixedReality.png" },
                new RemovableElement { DisplayName = "Windows Hello Face Recognition", PackageName = "Hello.Face.18967~~~~0.0.1.0", Description = "Removes biometric facial recognition payloads.", IsCapability = true, IconPath = null },
                new RemovableElement { DisplayName = "Windows Fax and Scan", PackageName = "Print.Fax.Scan~~~~0.0.1.0", Description = "Legacy dial-up faxing and scanning.", IsCapability = true, IconPath = "ms-appx:///Assets/ImagePackages/FaxScan.png" },
                new RemovableElement { DisplayName = "XPS Viewer", PackageName = "XPS.Viewer~~~~0.0.1.0", Description = "Legacy app used to view XPS documents.", IsCapability = true, IconPath = "ms-appx:///Assets/ImagePackages/XPSViewer.png"  },
                new RemovableElement { DisplayName = "Wireless Display (Miracast)", PackageName = "App.WirelessDisplay.Connect~~~~0.0.1.0", Description = "The 'Connect' app for wireless display.", IsCapability = true, IconPath = null },
                new RemovableElement { DisplayName = "OpenSSH Client", PackageName = "OpenSSH.Client~~~~0.0.1.0", Description = "Developer networking tool for Secure Shell.", IsCapability = true, IconPath = "ms-appx:///Assets/ImagePackages/Terminal.png" }
            };
        }

        #endregion
    }
}