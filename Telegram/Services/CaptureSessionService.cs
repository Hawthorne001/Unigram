﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Native.Composition;
using Telegram.Views.Popups;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.Display;
using Windows.Security.Authorization.AppCapabilityAccess;
using Windows.UI;
using Windows.UI.WindowManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Services
{
    public record CaptureSessionOptions(GraphicsCaptureItem CaptureItem, long ProcessId);

    public abstract record CaptureSessionItem(string DisplayName)
    {
        public abstract CaptureSessionOptions ToOptions(bool includeAudio);
    }

    public record WindowCaptureSessionItem(string DisplayName, WindowId WindowId) : CaptureSessionItem(DisplayName)
    {
        public override CaptureSessionOptions ToOptions(bool includeAudio)
        {
            var item = GraphicsCaptureItem.TryCreateFromWindowId(WindowId);
            if (item != null)
            {
                if (includeAudio)
                {
                    return new CaptureSessionOptions(item, WindowVisual.GetWindowProcessId(WindowId));
                }
                else
                {
                    return new CaptureSessionOptions(item, 0);
                }
            }

            return null;
        }
    }

    public record DisplayCaptureSessionItem(GraphicsCaptureItem Item, DisplayId DisplayId) : CaptureSessionItem(Item.DisplayName)
    {
        public static implicit operator GraphicsCaptureItem(DisplayCaptureSessionItem d) => d.Item;

        public override CaptureSessionOptions ToOptions(bool includeAudio)
        {
            return new CaptureSessionOptions(Item, includeAudio ? -1 : 0);
        }
    }

    public partial class CaptureSessionService
    {
        #region Choose

        public static async Task<CaptureSessionOptions> ChooseAsync(XamlRoot xamlRoot, bool canShareAudio)
        {
            var access = await RequestAccessAsync();
            if (access == AppCapabilityAccessStatus.UserPromptRequired)
            {
                var picker = new GraphicsCapturePicker();

                var backup = await picker.PickSingleItemAsync();
                if (backup != null)
                {
                    return new CaptureSessionOptions(backup, 0);
                }

                return null;
            }
            else if (access != AppCapabilityAccessStatus.Allowed)
            {
                return null;
            }

            WatchDog.MemoryStatus();

            var popup = new ChooseCapturePopup(canShareAudio);

            var confirm = await popup.ShowQueuedAsync(xamlRoot);
            if (confirm != ContentDialogResult.Primary)
            {
                return null;
            }

            if (popup.SelectedItem is CaptureSessionItem item)
            {
                return item.ToOptions(popup.IsAudioCaptureEnabled);
            }

            return null;
        }

        #endregion

        public static async Task<AppCapabilityAccessStatus> RequestAccessAsync()
        {
            if (ApiInfo.IsBuildOrGreater(20348))
            {
                try
                {
                    var capability = AppCapability.Create("graphicsCaptureProgrammatic");
                    var status = capability.CheckAccess();

                    if (status == AppCapabilityAccessStatus.UserPromptRequired)
                    {
                        var access = await AppCapability.RequestAccessForCapabilitiesAsync(new[] { "graphicsCaptureProgrammatic" });
                        if (access.TryGetValue("graphicsCaptureProgrammatic", out status))
                        {
                            return status;
                        }
                    }
                    else if (status != AppCapabilityAccessStatus.Allowed)
                    {
                        status = AppCapabilityAccessStatus.UserPromptRequired;
                    }

                    return status;
                }
                catch
                {
                    // All the remote procedure calls must be wrapped in a try-catch block
                }
            }

            return AppCapabilityAccessStatus.UserPromptRequired;
        }

        public static IList<CaptureSessionItem> FindAll()
        {
            var items = new List<CaptureSessionItem>();
            items.AddRange(FindAllDisplayIds());
            items.AddRange(FindAllTopLevelWindowIds());

            return items;
        }

        public static IList<CaptureSessionItem> FindAllTopLevelWindowIds()
        {
            var windows = WindowServices.FindAllTopLevelWindowIds();
            var items = new List<CaptureSessionItem>();

            var current = WindowVisual.GetCurrentWindowId();

            foreach (var window in windows)
            {
                if (window.Value == current.Value)
                {
                    continue;
                }

                var item = GetCaptureSessionItem(window);
                if (item != null)
                {
                    items.Add(item);
                }
            }

            return items;
        }

        public static IList<CaptureSessionItem> FindAllDisplayIds()
        {
            var displays = DisplayServices.FindAll();
            var items = new List<CaptureSessionItem>();

            foreach (var display in displays)
            {
                var item = GraphicsCaptureItem.TryCreateFromDisplayId(display);
                if (item != null)
                {
                    items.Add(new DisplayCaptureSessionItem(item, display));
                }
            }

            return items;
        }

        private static CaptureSessionItem GetCaptureSessionItem(WindowId windowId)
        {
            if (WindowVisual.IsValid(windowId, out string title))
            {
                return new WindowCaptureSessionItem(title, windowId);
            }

            return null;
        }
    }
}
