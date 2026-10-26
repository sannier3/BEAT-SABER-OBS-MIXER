using System;
using CP_SDK;
using CP_SDK.UI;
using CP_SDK.UI.Components;
using OBSMixer.Core;
using UnityEngine;

namespace OBSMixer
{
    public sealed partial class MixerRuntime
    {
        internal ObsPreview Preview { get; private set; }
        private CFloatingPanel previewPanel;
        private PreviewView previewView;
        private bool previewRestoreLock;
        private void InitializePreview()
        {
            Preview = new ObsPreview(Connection);
            Preview.Diagnostic += LogDiagnostic;
        }
        internal void SetPreviewEnabled(bool enabled)
        {
            SavePose(); Config.PreviewPanelEnabled = enabled; ApplySettings();
        }
        internal void TogglePreviewImage()
        {
            Config.PreviewImageVisible = !Config.PreviewImageVisible;
            SaveSoon(); ApplyPreviewVisibility();
        }
        private void ApplyPreviewSettings()
        {
            Preview.IntervalMs = Config.PreviewIntervalMs;
            if (Config.PreviewPanelEnabled && !previewPanel)
            {
                previewPanel = UISystem.FloatingPanelFactory.Create("OBS Program preview", null);
                DontDestroyOnLoad(previewPanel.gameObject);
                previewPanel.gameObject.SetActive(false);
                previewPanel.SetSize(new Vector2(140, 104)).SetLockIcon(CFloatingPanel.ECorner.TopRight);
                previewPanel.OnSceneRelease(EGenericScene.Menu, (p, r) => PreviewReleased(p, r));
                previewPanel.OnSceneRelease(EGenericScene.Playing, (p, r) => PreviewReleased(p, r));
                previewView = UISystem.CreateViewController<PreviewView>(); previewView.Runtime = this;
                previewPanel.SetViewController(previewView);
                ApplyPreviewPose();
            }
            if (!previewPanel) return;
            previewPanel.SetRadius(Config.CurvatureRadius)
                .SetBackground(true, new Color(.025f, .04f, .07f, Config.BackgroundOpacity));
            previewPanel.RTransform.localScale = Vector3.one * (.02f * Config.PanelScale);
            if (previewPanel.GetAllowMovement() == Config.PreviewPanelLocked) previewPanel.SetAllowMovement(!Config.PreviewPanelLocked);
        }
        private void ApplyPreviewPose()
        {
            if (!previewPanel) return;
            var pose = Config.PreviewPose;
            previewPanel.SetSceneTransform(EGenericScene.Menu, pose.Position, pose.Rotation);
            previewPanel.SetSceneTransform(EGenericScene.Playing, pose.Position, pose.Rotation);
            previewPanel.SetAllowMovement(!Config.PreviewPanelLocked);
            previewRestoreLock = true;
        }
        private void PreviewReleased(Vector3 position, Vector3 rotation)
        {
            if (visibility.ContextVisible && Config.PreviewPanelEnabled)
            { Config.PreviewPose.Set(position, rotation); SaveSoon(); }
            ApplyPreviewPose();
        }
        private void SavePreviewPose()
        {
            if (!previewPanel || !previewPanel.gameObject.activeInHierarchy || !visibility.ContextVisible) return;
            Config.PreviewPose.Set(previewPanel.RTransform.localPosition, previewPanel.RTransform.localEulerAngles);
            SaveSoon(); ApplyPreviewPose();
        }
        private void ApplyPreviewVisibility()
        {
            bool visible = visibility.ContextVisible && Config.PreviewPanelEnabled;
            Preview.Active = visible && Config.PreviewImageVisible;
            if (previewPanel) previewPanel.gameObject.SetActive(visible);
            if (visible && previewView) previewView.RenderState();
        }
        private void UpdatePreview()
        {
            // Panels created later subscribe after the runtime: restore the saved lock
            // after ChatPlex's automatic scene-lock handler has run.
            if (previewPanel && previewRestoreLock)
            { previewRestoreLock = false; previewPanel.SetAllowMovement(!Config.PreviewPanelLocked); }
            if (previewPanel && previewPanel.gameObject.activeInHierarchy)
            {
                if (Config.PreviewPanelLocked == previewPanel.GetAllowMovement())
                { Config.PreviewPanelLocked = !previewPanel.GetAllowMovement(); SaveSoon(); }
                previewView.RenderState();
            }
            if (Preview.Active) _ = Preview.RefreshAsync(DateTime.UtcNow);
        }
        private void DestroyPreview()
        {
            if (Preview != null) { Preview.Diagnostic -= LogDiagnostic; Preview.Dispose(); }
            if (previewPanel) UISystem.DestroyUI(ref previewPanel, ref previewView);
        }
    }
}
