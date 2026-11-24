using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LanSceneReplicator : MonoBehaviour
{
    public static LanSceneReplicator Instance { get; private set; }

    private readonly Dictionary<string, TilesMovement> figuresById = new Dictionary<string, TilesMovement>(StringComparer.Ordinal);
    public bool IsReady { get; private set; }
    public string ValidationError { get; private set; } = string.Empty;
    public string LayoutSignature { get; private set; } = string.Empty;

    private void Awake()
    {
        Instance = this;
        Refresh();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Invalid(string validationError)
    {
        figuresById.Clear();
        IsReady = false;
        ValidationError = validationError.Length > 200 ? validationError.Substring(0, 200) : validationError;
    }

    public void Refresh()
    {
        figuresById.Clear();
        IsReady = false;
        ValidationError = LayoutSignature = string.Empty;
        string scenePath = SceneManager.GetActiveScene().path;
        foreach (TilesMovement figureMovement in FindObjectsByType<TilesMovement>(FindObjectsSortMode.None))
        {
            if (figureMovement.gameObject.scene.path != scenePath)
            {
                continue;
            }

            if (!figureMovement.TryGetComponent(out FigureTurn figureTurn))
            {
                Invalid(figureMovement.name + ": отсутствует FigureTurn.");
                return;
            }

            if (figureTurn.Movement != figureMovement)
            {
                Invalid(figureMovement.name + ": Tiles Movement в FigureTurn ссылается на другую фигуру.");
                return;
            }

            if ((int)figureTurn.Team < 0 || (int)figureTurn.Team > 3)
            {
                Invalid(figureMovement.name + ": неверная команда FigureTurn.");
                return;
            }

            string figureId = figureTurn.NetworkId;
            if (string.IsNullOrWhiteSpace(figureId) || figureId.Length > 64
                || figureId.Any(character => !char.IsLetterOrDigit(character) && character != '-' && character != '_'))
            {
                Invalid(figureMovement.name + ": задай Network Id в FigureTurn (до 64 букв, цифр, - или _).");
                return;
            }

            if (figuresById.ContainsKey(figureId))
            {
                Invalid("Повтор Network Id " + figureId + ": " + figuresById[figureId].name + " и " + figureMovement.name + ".");
                return;
            }

            figuresById.Add(figureId, figureMovement);
        }

        if (figuresById.Count == 0)
        {
            Invalid("В сцене нет управляемых фигур.");
            return;
        }

        var figureDescriptions = new List<string>();
        foreach (var figureEntry in figuresById.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            TilesMovement figureMovement = figureEntry.Value;
            int teamNumber = (int)figureMovement.GetComponent<FigureTurn>().Team;
            string description = figureEntry.Key
                + ":" + teamNumber.ToString(CultureInfo.InvariantCulture)
                + ":" + figureMovement.name.Length.ToString(CultureInfo.InvariantCulture)
                + ":" + figureMovement.name;
            figureDescriptions.Add(description);
        }

        string layout = string.Join("\n", figureDescriptions);
        using (SHA256 hash = SHA256.Create())
        {
            LayoutSignature = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(layout))).Replace("-", string.Empty);
        }

        IsReady = true;
    }

    public void RegisterFigure(TilesMovement figureMovement)
    {
        if (figureMovement != null && figureMovement.TryGetComponent(out FigureTurn figureTurn))
        {
            figuresById[figureTurn.NetworkId] = figureMovement;
        }
    }

    public bool HasTeam(TurnManager.Team team)
    {
        return IsReady
            && figuresById.Values.Any(figureMovement => figureMovement != null && figureMovement.GetComponent<FigureTurn>().Team == team);
    }

    public bool TryGetId(TilesMovement figureMovement, out string figureId)
    {
        figureId = string.Empty;
        if (!IsReady || figureMovement == null || !figureMovement.TryGetComponent(out FigureTurn figureTurn))
        {
            return false;
        }

        figureId = figureTurn.NetworkId;
        return figuresById.TryGetValue(figureId, out TilesMovement registered) && registered == figureMovement;
    }

    public TilesMovement GetFigure(string figureId)
    {
        return IsReady && figureId != null && figuresById.TryGetValue(figureId, out TilesMovement figureMovement)
            ? figureMovement
            : null;
    }

    public bool ApplyNetworkMove(string figureId, int targetX, int targetY)
    {
        TilesMovement figureMovement = GetFigure(figureId);
        if (figureMovement == null)
        {
            return false;
        }

        figureMovement.ApplyNetworkPosition(targetX, targetY);
        return true;
    }
}
