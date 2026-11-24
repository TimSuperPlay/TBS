using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

public class BuildingFoundation : MonoBehaviour, IResourceReceiver
{
    [SerializeField]
    [FormerlySerializedAs("buildingsToBuild")]
    private BuildingToBuild[] buildingRecipes;
    private GameObject mapManager;
    private List<string> storedResources = new();
    public string[] Inputs
    {
        get
        {
            return storedResources.ToArray();
        }
    }

    public void ApplyInputs(string[] receivedResources)
    {
        storedResources = new List<string>(receivedResources ?? Array.Empty<string>());
    }

    public IEnumerable<GameObject> Prefabs
    {
        get
        {
            return buildingRecipes == null ? Enumerable.Empty<GameObject>() : buildingRecipes.Select(recipe => recipe.buildingPrefab);
        }
    }

    public void AddResource(string resourceName)
    {
        if (!LanGameState.CanSimulate)
        {
            return;
        }

        storedResources.Add(resourceName);
        TryProduce();
    }

    private void Update()
    {
        if (LanGameState.CanSimulate && storedResources.Count > 0)
        {
            TryProduce();
        }
    }

    private void TryProduce()
    {
        if (buildingRecipes == null)
        {
            return;
        }

        foreach (var recipe in buildingRecipes)
        {
            if (!LanWorldReplicator.CanSpawn(recipe.buildingPrefab)
                || !LanWorldReplicator.HasResources(storedResources, recipe.resourcesNeeded))
            {
                continue;
            }

            var tilesController = FindFirstObjectByType<TilesController>();
            var tilesGenerator = FindFirstObjectByType<TilesGenerator>();
            if (tilesController == null || tilesGenerator == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(recipe.tileName)
                && (!tilesGenerator.TryGetWorldTile(transform.position, out TileConfig tile) || tile.tileName != recipe.tileName))
            {
                continue;
            }

            var replacement = LanWorldReplicator.Spawn(recipe.buildingPrefab, transform.position, LanWorldReplicator.TeamOf(gameObject));
            if (replacement == null)
            {
                return;
            }

            foreach (string recipeResource in recipe.resourcesNeeded)
            {
                storedResources.Remove(recipeResource);
            }

            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
    }

    [Serializable]
    private class BuildingToBuild
    {
        [Description("If empty can be build anywhere")]
        public string tileName;
        public GameObject buildingPrefab;
        public string[] resourcesNeeded;
    }
}
