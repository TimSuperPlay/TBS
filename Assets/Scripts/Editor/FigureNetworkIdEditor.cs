using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TBS.Editor
{
    [InitializeOnLoad]
    internal static class FigureNetworkIdEditor
    {
        private const string IdProperty = "networkId";
        private static readonly Dictionary<FigureTurn, string> KnownIds = new Dictionary<FigureTurn, string>();
        private static bool scanQueued;
        private static bool scanning;
        static FigureNetworkIdEditor()
        {
            EditorApplication.hierarchyChanged += QueueScan;
            ObjectChangeEvents.changesPublished += ObjectsChanged;
            Undo.undoRedoPerformed += QueueScan;
            EditorSceneManager.sceneOpened += SceneOpened;
            EditorSceneManager.sceneSaving += SceneSaving;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            QueueScan();
        }

        private static void ObjectsChanged(ref ObjectChangeEventStream changes)
        {
            QueueScan();
        }

        private static void SceneOpened(Scene scene, OpenSceneMode mode)
        {
            QueueScan();
        }

        private static void SceneSaving(Scene scene, string path)
        {
            if (!EditorApplication.isPlaying && !BuildPipeline.isBuildingPlayer)
            {
                RepairLoadedScenes();
            }
        }

        private static void PlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                RepairLoadedScenes(true);
            }

            if (state == PlayModeStateChange.EnteredEditMode)
            {
                QueueScan();
            }
        }

        private static void QueueScan()
        {
            if (scanQueued || scanning || EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            {
                return;
            }

            scanQueued = true;
            EditorApplication.delayCall += ScanLater;
        }

        private static void ScanLater()
        {
            scanQueued = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                QueueScan();
                return;
            }

            RepairLoadedScenes();
        }

        [MenuItem("Tools/TBS/Assign missing figure IDs")]
        private static void RepairFromMenu()
        {
            int updatedFigureCount = RepairLoadedScenes();
            Debug.Log("[LAN] Назначено ID: " + updatedFigureCount + ". Сохрани сцену (Ctrl+S) и пересобери игру для всех игроков.");
        }

        internal static int RepairLoadedScenes(bool enteringPlayMode = false)
        {
            if (scanning || EditorApplication.isPlaying || BuildPipeline.isBuildingPlayer
                || (EditorApplication.isPlayingOrWillChangePlaymode && !enteringPlayMode))
            {
                return 0;
            }

            scanning = true;
            int updatedFigureCount = 0;
            try
            {
                foreach (FigureTurn removedFigure in KnownIds.Keys.Where(knownFigure => knownFigure == null).ToArray())
                {
                    KnownIds.Remove(removedFigure);
                }

                for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
                {
                    Scene scene = SceneManager.GetSceneAt(sceneIndex);
                    if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene))
                    {
                        continue;
                    }

                    List<FigureTurn> figures = SceneFigures(scene)
                        .Where(figure => !EditorUtility.IsPersistent(figure)
                            && PrefabStageUtility.GetPrefabStage(figure.gameObject) == null)
                        .ToList();
                    figures = figures.OrderByDescending(figure => KnownIds.TryGetValue(figure, out string previousId)
                        && string.Equals(previousId, figure.NetworkId, StringComparison.Ordinal))
                        .ToList();
                    var reserved = new HashSet<string>(figures.Select(figure => figure.NetworkId).Where(ValidId), StringComparer.Ordinal);
                    var claimed = new HashSet<string>(StringComparer.Ordinal);
                    bool sceneChanged = false;
                    foreach (FigureTurn figure in figures)
                    {
                        var serializedFigure = new SerializedObject(figure);
                        SerializedProperty networkIdProperty = serializedFigure.FindProperty(IdProperty);
                        if (networkIdProperty == null)
                        {
                            Debug.LogError("[LAN] " + figure.name + ": нужен FigureTurn из сетевого пакета v2.", figure);
                            continue;
                        }

                        string figureId = networkIdProperty.stringValue;
                        bool idInheritedFromPrefab = PrefabUtility.IsPartOfPrefabInstance(figure)
                            && PrefabUtility.GetCorrespondingObjectFromSource(figure) != null
                            && !networkIdProperty.prefabOverride;
                        if (!ValidId(figureId) || idInheritedFromPrefab || !claimed.Add(figureId))
                        {
                            do
                            {
                                figureId = Guid.NewGuid().ToString("N");
                            }
                            while (!reserved.Add(figureId));
                            networkIdProperty.stringValue = figureId;
                            serializedFigure.ApplyModifiedPropertiesWithoutUndo();
                            if (PrefabUtility.IsPartOfPrefabInstance(figure))
                            {
                                PrefabUtility.RecordPrefabInstancePropertyModifications(figure);
                            }

                            EditorUtility.SetDirty(figure);
                            claimed.Add(figureId);
                            updatedFigureCount++;
                            sceneChanged = true;
                        }

                        KnownIds[figure] = figureId;
                    }

                    if (sceneChanged)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                    }
                }
            }
            finally
            {
                scanning = false;
            }

            return updatedFigureCount;
        }

        internal static IEnumerable<FigureTurn> SceneFigures(Scene scene)
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FigureTurn>(true));
        }

        internal static bool ValidId(string figureId)
        {
            return !string.IsNullOrWhiteSpace(figureId) && figureId.Length <= 64
                && figureId.All(character => char.IsLetterOrDigit(character) || character == '-' || character == '_');
        }

        internal static void ValidateSavedScene(Scene scene)
        {
            var existingIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (FigureTurn figure in SceneFigures(scene))
            {
                if (!ValidId(figure.NetworkId) || !existingIds.Add(figure.NetworkId))
                {
                    throw new BuildFailedException("[LAN] " + scene.path + ": пустой, неверный или повторный Network Id у " + figure.name
                        + ". Открой сцену, выбери Tools > TBS > Assign missing figure IDs, сохрани и повтори сборку.");
                }
            }
        }
    }

    public sealed class FigureNetworkIdBuildCheck : IProcessSceneWithReport
    {
        public int callbackOrder
        {
            get
            {
                return 0;
            }
        }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report != null)
            {
                FigureNetworkIdEditor.ValidateSavedScene(scene);
            }
        }
    }
}
