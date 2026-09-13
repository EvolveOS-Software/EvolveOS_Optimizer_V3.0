// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

#region System Namespaces
global using System;
global using System.Collections.Generic;
global using System.Diagnostics;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
#endregion

#region Toolkit & MVVM
global using CommunityToolkit.Mvvm.ComponentModel;
global using CommunityToolkit.Mvvm.Input;
#endregion

#region EvolveOS Optimizer Namespaces
global using EvolveOS_Optimizer.Core.Constants;
global using EvolveOS_Optimizer.Core.Enums;
global using EvolveOS_Optimizer.Core.Events;
global using EvolveOS_Optimizer.Core.Interfaces;
global using EvolveOS_Optimizer.Core.Model;
global using EvolveOS_Optimizer.Core.ViewModel;
global using EvolveOS_Optimizer.Pages;
global using EvolveOS_Optimizer.Utilities.Animation;
global using EvolveOS_Optimizer.Utilities.Configuration;
global using EvolveOS_Optimizer.Utilities.Controls;
global using EvolveOS_Optimizer.Utilities.Extensions;
global using EvolveOS_Optimizer.Utilities.Helpers;
global using EvolveOS_Optimizer.Utilities.Maintenance;
global using EvolveOS_Optimizer.Utilities.Managers;
global using EvolveOS_Optimizer.Utilities.Services;
#endregion

#region WinUI 3 / Microsoft Namespaces
global using Microsoft.UI;
global using Microsoft.UI.Xaml;
global using Microsoft.UI.Xaml.Controls;
global using Microsoft.UI.Xaml.Media;
global using Microsoft.UI.Xaml.Media.Imaging;
global using Microsoft.UI.Xaml.Navigation;
global using Windows.Foundation;
#endregion

#region Global Aliases & Statics
global using static EvolveOS_Optimizer.Utilities.Helpers.Win32Helper;

global using Color = global::Windows.UI.Color;
global using ColorHelper = Microsoft.UI.ColorHelper;
global using Colors = Microsoft.UI.Colors;
global using File = System.IO.File;
global using Memory = EvolveOS_Optimizer.Core.Model.MemoryModel.Memory;
global using Registry = Microsoft.Win32.Registry;
global using VirtualKey = global::Windows.System.VirtualKey;
global using XmlDocument = global::Windows.Data.Xml.Dom.XmlDocument;
#endregion