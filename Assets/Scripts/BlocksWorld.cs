using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class BlocksWorld : MonoBehaviour
{
    /*
     * Ontology:
     *   On(x, y)     --- block x is on top of block y
     *   OnTable(x)   --- block x is on the hallOfFameTable
     *   Clear(x)     --- nothing is on top of block x
     */

    [Header("Block Settings")]
    [SerializeField] private GameObject blockPrefab;
    [SerializeField] private GameObject canvas;
    public static int numberOfBlocks = 4;

    // Canvas-space spacing (in pixels if using Screen Space)
    [SerializeField] private float horizontalSpacing = 150f;
    [SerializeField] private float verticalSpacing = 80f;
    [SerializeField] private float tableY = -150f;  // base Y for the "hallOfFameTable"

    // FOL-style representation of the CURRENT world
    // On(x, y) is encoded as: on[x] = y  (y is either another block name or TABLE)
    private Dictionary<string, string> on = new Dictionary<string, string>();

    // Clear(x) is represented as a set of block names that are clear.
    private HashSet<string> clear = new HashSet<string>();

    // FOL-style representation of a GOAL world (independent, for now only printed)
    private Dictionary<string, string> goalOn = new Dictionary<string, string>();
    private HashSet<string> goalClear = new HashSet<string>();

    // Block data structure
    private class BlockData
    {
        public string name;
        public GameObject instance;
    }

    private readonly Dictionary<string, BlockData> blocks = new Dictionary<string, BlockData>();
    private readonly List<string> blockNames = new();

    // Constant for hallOfFameTable representation
    private const string TABLE = "Table";

    // Has the goal configuration been achieved?
    public bool goalReached = false;


    public void Initialize()
    {
        canvas = this.gameObject;
        if (blockPrefab == null)
        {
            Debug.LogError("BlocksWorldManager: blockPrefab is not assigned.");
            return;
        }

        if (canvas == null)
        {
            Debug.LogError("BlocksWorldManager: canvas is not assigned.");
            return;
        }

        InitializeBlocks();

        // 1. Generate goal state
        GenerateNonFlatGoal();

        // 2. Generate an initial state that is as far as possible from the goal
        GenerateFarInitialFromGoal();

        // 3. Lay out initial state and print goal
        LayoutBlocksFromState(on);
    }


    public float GetFitnessForWorldState()
    {
        GetCurrentStateNoClear(out var curstate);
        GetGoalStateNoClear(out var goalstate);
        int matches = CountMatchingOn(on, goalOn);
        if (curstate == goalstate)
        {
            return matches*10;
        }
        else
        {
            return matches;
        }
           
    }

    private void GenerateNonFlatGoal()
    {
        while (true)
        {
            GenerateRandomState(goalOn, goalClear);

            bool allOnTable = true;
            foreach (var b in blocks.Keys)
            {
                if (goalOn[b] != TABLE)
                {
                    allOnTable = false;
                    break;
                }
            }

            if (!allOnTable)
                break; // accept this goal
        }
    }


    public static string GetBlockName(int i)
    {
        char labelChar = (char)('A' + i);
        return labelChar.ToString();
    }

    #region Initialization

    private void InitializeBlocks()
    {
        blocks.Clear();
        on.Clear();
        clear.Clear();

        for (int i = 0; i < numberOfBlocks; i++)
        {
            string blockName = GetBlockName(i);

            GameObject blockGO = Instantiate(blockPrefab, Vector3.zero, Quaternion.identity, canvas.transform);
            blockGO.name = $"Block_{blockName}";
            var rt = blockGO.GetComponent<RectTransform>();
            rt.SetParent(canvas.GetComponent<RectTransform>(), false);
            rt.anchoredPosition = Vector2.zero;

            // Get the Image component and color it deterministically by letter
            var image = blockGO.GetComponent<Image>();
            if (image != null)
            {
                image.color = GetColorForBlock(blockName);
            }

            // Label via TextMeshProUGUI (preferred)
            var labelUGUI = blockGO.GetComponentInChildren<TextMeshProUGUI>();
            if (labelUGUI != null)
            {
                labelUGUI.text = blockName;
            }
            else
            {
                // fallback for TextMeshPro (world-space)
                var labelTMP = blockGO.GetComponentInChildren<TextMeshPro>();
                if (labelTMP != null)
                    labelTMP.text = blockName;
            }

            BlockData data = new BlockData
            {
                name = blockName,
                instance = blockGO
            };

            blocks[blockName] = data;
            blockNames.Add(blockName);
        }
    }

    private Color GetColorForBlock(string blockName)
    {
        if (string.IsNullOrEmpty(blockName))
            return Color.white;

        char c = blockName[0];

        switch (c)
        {
            case 'A': return Color.red;
            case 'B': return Color.blue;
            case 'C': return Color.green;
            case 'D': return Color.yellow;
            case 'E': return new Color(1f, 0.5f, 0f); // orange
            case 'F': return Color.cyan;
            // add more if you like…
            default:
                // fallback: deterministic color based on letter index
                int idx = c - 'A';
                float hue = (idx * 0.13f) % 1f;
                return Color.HSVToRGB(hue, 0.7f, 0.9f);
        }
    }


    #endregion

    #region FOL-style state generation

    /// <summary>
    /// Generate a random valid blocks world configuration:
    /// Each block is either on the hallOfFameTable (OnTable) or stacked on some clear block.
    /// This fills the given 'onState' (for On) and 'clearState' (for Clear) collections.
    /// </summary>
    private void GenerateRandomState(Dictionary<string, string> onState, HashSet<string> clearState)
    {
        onState.Clear();
        clearState.Clear();

        // Start with supports = { Table }
        List<string> supports = new List<string> { TABLE };

        // Random order of blocks
        List<string> localBlockNames = new List<string>(blocks.Keys);
        Shuffle(localBlockNames);

        foreach (string block in localBlockNames)
        {
            // Choose a random support from current supports (Table or a clear block)
            int idx = Random.Range(0, supports.Count);
            string support = supports[idx];

            // On(block, support)
            onState[block] = support;

            // If support is a block, it is no longer clear
            if (support != TABLE)
            {
                supports.Remove(support);
                clearState.Remove(support);
            }

            // The new block itself is now clear (top of its stack)
            supports.Add(block);
            clearState.Add(block);
        }
    }

    #endregion

    #region Layout (Canvas)

    /// <summary>
    /// Position the GameObjects according to the given 'onState' mapping
    /// using RectTransform.anchoredPosition on the Canvas.
    /// </summary>
    private void LayoutBlocksFromState(Dictionary<string, string> onState)
    {
        if (canvas == null) return;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        if (canvasRect == null)
        {
            Debug.LogError("BlocksWorldManager: Canvas object has no RectTransform.");
            return;
        }

        // Find all blocks directly on the hallOfFameTable (roots of stacks)
        List<string> roots = new List<string>();
        foreach (var kvp in onState)
        {
            string block = kvp.Key;
            string support = kvp.Value;
            if (support == TABLE) // OnTable(block)
            {
                roots.Add(block);
            }
        }

        if (roots.Count == 0) return;

        // Place roots along X axis in canvas space (centered)
        float totalWidth = (roots.Count - 1) * horizontalSpacing;
        float startX = -totalWidth / 2f;

        for (int i = 0; i < roots.Count; i++)
        {
            string root = roots[i];
            float x = startX + i * horizontalSpacing;
            Vector2 rootPos = new Vector2(x, tableY);
            SetBlockPosition(root, rootPos);

            // Recursively place any blocks above this one
            PlaceStackAbove(root, onState);
        }
    }

    /// <summary>
    /// Recursively position blocks that are on top of 'supportBlock' in canvas space.
    /// </summary>
    private void PlaceStackAbove(string supportBlock, Dictionary<string, string> onState)
    {
        Vector2 supportPos = GetBlockPosition(supportBlock);
        float yTop = supportPos.y + verticalSpacing;

        // Find any block that is on top of 'supportBlock'
        foreach (var kvp in onState)
        {
            string block = kvp.Key;
            string support = kvp.Value;
            if (support == supportBlock)
            {
                // Place this block just above the support
                Vector2 childPos = new Vector2(supportPos.x, yTop);
                SetBlockPosition(block, childPos);

                // Recursively place any blocks stacked on it
                PlaceStackAbove(block, onState);
            }
        }
    }

    private void SetBlockPosition(string blockName, Vector2 anchoredPos)
    {
        if (blocks.TryGetValue(blockName, out BlockData data))
        {
            RectTransform rt = data.instance.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = anchoredPos;
            }
            else
            {
                data.instance.transform.localPosition = new Vector3(anchoredPos.x, anchoredPos.y, 0f);
            }
        }
    }

    private Vector2 GetBlockPosition(string blockName)
    {
        if (blocks.TryGetValue(blockName, out BlockData data))
        {
            RectTransform rt = data.instance.GetComponent<RectTransform>();
            if (rt != null)
            {
                return rt.anchoredPosition;
            }
            return data.instance.transform.localPosition;
        }
        return Vector2.zero;
    }

    #endregion

    #region FOL helper predicates

    /// <summary>
    /// FOL predicate: On(x, y) --- block x is on top of block y in the current state.
    /// </summary>
    public bool OnPredicate(string x, string y)
    {
        return on.TryGetValue(x, out string support) && support == y;
    }

    /// <summary>
    /// FOL predicate: OnTable(x) --- block x is on the hallOfFameTable.
    /// Implemented as On(x, Table).
    /// </summary>
    public bool OnTablePredicate(string x)
    {
        return OnPredicate(x, TABLE);
    }

    /// <summary>
    /// FOL predicate: Clear(x) --- nothing is on top of block x.
    /// </summary>
    public bool ClearPredicate(string x)
    {
        return clear.Contains(x);
    }

    /// <summary>
    /// Recompute the 'clear' set from the current 'on' mapping.
    /// Clear(x) is true iff no block has support = x.
    /// </summary>
    private void RecomputeClear()
    {
        clear.Clear();
        // Initially assume all blocks are clear
        foreach (string block in blocks.Keys)
        {
            clear.Add(block);
        }

        // Any block that appears as a support is not clear
        foreach (var kvp in on)
        {
            string support = kvp.Value;
            if (support != TABLE) // hallOfFameTable doesn't count as a block
            {
                clear.Remove(support);
            }
        }
    }

    /// <summary>
    /// Counts how many On(x, y) facts match between two states.
    /// Higher score = more similar stack structure.
    /// </summary>
    private int CountMatchingOn(
        Dictionary<string, string> onA,
        Dictionary<string, string> onB)
    {
        int score = 0;

        foreach (var kvp in blocks)
        {
            string block = kvp.Key;

            if (onA.TryGetValue(block, out string supportA) &&
                onB.TryGetValue(block, out string supportB) &&
                supportA == supportB)
            {
                score++;
            }
        }

        return score;
    }


    /// <summary>
    /// Fill 'on' and 'clear' with a random state that is as far as possible
    /// from the current goalOn/goalClear according to CountMatchingPredicates.
    /// </summary>
    private void GenerateFarInitialFromGoal()
    {
        const int maxTries = 100000;  // tweak this if you like
        int bestScore = int.MaxValue;

        // Temporary best copies
        Dictionary<string, string> bestOn = new Dictionary<string, string>();
        HashSet<string> bestClear = new HashSet<string>();

        for (int i = 0; i < maxTries; i++)
        {
            // Generate a random candidate into the real 'on' and 'clear'
            GenerateRandomState(on, clear);

            int score = CountMatchingOn(on, goalOn);

            if (score < bestScore)
            {
                bestScore = score;

                // Copy current 'on' and 'clear' into bestOn/bestClear
                bestOn.Clear();
                foreach (var kvp in on)
                {
                    bestOn[kvp.Key] = kvp.Value;
                }

                bestClear = new HashSet<string>(clear);

                // Perfect opposition under this metric – stop early
                if (bestScore == 0)
                    break;
            }
        }

        if(bestScore != 0)
        {
            Debug.LogError("initial state has some correct states");
        }

        // Copy best back into the actual current state
        on.Clear();
        foreach (var kvp in bestOn)
        {
            on[kvp.Key] = kvp.Value;
        }

        clear.Clear();
        foreach (var b in bestClear)
        {
            clear.Add(b);
        }

        Debug.Log($"Chosen initial state with similarity score {bestScore} (0 = completely opposite under our metric).");
    }




    #endregion

    #region Actions: Stack and Unstack

    /// <summary>
    /// Stack(top, bottom): place 'top' on 'bottom'.
    /// Valid iff both blocks are Clear.
    /// </summary>
    public bool Stack(string top, string bottom)
    {
        if (goalReached)
        {
            Debug.LogWarning("Stack ignored: goal state already reached.");
            return false;
        }

        if (top == bottom)
        {
            Debug.LogWarning("Stack invalid: cannot stack a block on itself.");
            return false;
        }

        if (!blocks.ContainsKey(top) || !blocks.ContainsKey(bottom))
        {
            Debug.LogWarning($"Stack invalid: unknown block(s) {top}, {bottom}.");
            return false;
        }

        if (OnPredicate(top, bottom))
        {
            Debug.LogWarning($"Stack invalid: blocks already stacked {top}, {bottom}.");
            return false;
        }

        if (!ClearPredicate(top) || !ClearPredicate(bottom))
        {
            Debug.LogWarning($"Stack invalid: both {top} and {bottom} must be Clear.");
            return false;
        }

        // Update FOL state: On(top, bottom)
        on[top] = bottom;

        // Recompute Clear and reposition blocks visually
        RecomputeClear();
        LayoutBlocksFromState(on);

        Debug.Log($"Action: Stack({top}, {bottom})");

        // <-- check goal AFTER applying the action
        CheckGoalReached();

        return true;
    }


    /// <summary>
    /// Unstack(block): take 'block' and put it on the hallOfFameTable.
    /// Valid iff 'block' is Clear.
    /// This corresponds to: OnTable(block).
    /// </summary>
    public bool Unstack(string block)
    {
        if (goalReached)
        {
            Debug.LogWarning("Unstack ignored: goal state already reached.");
            return false;
        }

        if (!blocks.ContainsKey(block))
        {
            Debug.LogWarning($"Unstack invalid: unknown block {block}.");
            return false;
        }

        if (!ClearPredicate(block))
        {
            Debug.LogWarning($"Unstack invalid: block {block} must be Clear.");
            return false;
        }

        if (OnTablePredicate(block))
        {
            Debug.LogWarning($"Unstack invalid: block {block} is already on Table.");
            return false;
        }

        // OnTable(block) == On(block, Table)
        on[block] = TABLE;

        // Recompute Clear and reposition blocks visually
        RecomputeClear();
        LayoutBlocksFromState(on);

        Debug.Log($"Action: Unstack({block})  // OnTable({block})");

        // <-- check goal AFTER applying the action
        CheckGoalReached();

        return true;
    }


    #endregion

    #region Goal state printing

    List<StatementTerm> goal_states;
    public List<StatementTerm> GetGoalStateNoClear(out string str)
    {
        StringBuilder sb = new StringBuilder();
       // sb.Append("Random goal state (FOL): ");

        goal_states = new List<StatementTerm>(blocks.Count);
        // On / OnTable for EVERY block
        foreach (var kvp in blockNames)
        {
            string block = kvp;

            if (!goalOn.TryGetValue(block, out string support))
            {
                // Shouldn't happen, but just in case
                continue;
            }

            if (support == TABLE)
            {
                sb.Append($" ({block} --> OnTable)");
                goal_states.Add(StatementTerm.from_string($"({block} --> OnTable)"));
            }

            else
            {
                sb.Append($" ((*,{block}, {support}) --> On)");
                goal_states.Add(StatementTerm.from_string($"((*,{block}, {support}) --> On)"));
            }

        }

        //// Clear info for EVERY block (either Clear or ¬Clear)
        //foreach (var kvp in blocks)
        //{
        //    string block = kvp.Key;

        //    if (goalClear.Contains(block))
        //        sb.Append($" ({block} --> Clear)");
        //}

      //  Debug.Log(;
        str = sb.ToString();

        return goal_states;
    }

    #region Current state printing

    List<StatementTerm> current_states;


    public List<StatementTerm> GetCurrentStateNoClear(out string str)
    {
        StringBuilder sb = new StringBuilder();
        current_states = new List<StatementTerm>(blocks.Count);

        // On / OnTable for EVERY block in the *current* state
        foreach (var kvp in blockNames)
        {
            string block = kvp;

            if (!on.TryGetValue(block, out string support))
            {
                // If the current 'on' mapping doesn't have this block yet, skip
                continue;
            }

            if (support == TABLE)
            {
                sb.Append($" ({block} --> OnTable)");
                current_states.Add(StatementTerm.from_string($"({block} --> OnTable)"));
            }
            else
            {
                sb.Append($" ((*,{block}, {support}) --> On)");
                current_states.Add(StatementTerm.from_string($"((*,{block}, {support}) --> On)"));
            }
        }
        str = sb.ToString();
        return current_states;
    }

    public List<StatementTerm> GetCurrentState(out string str)
    {
        StringBuilder sb = new StringBuilder();
        current_states = new List<StatementTerm>(blocks.Count);

        // On / OnTable for EVERY block in the *current* state
        foreach (var kvp in blockNames)
        {
            string block = kvp;

            if (!on.TryGetValue(block, out string support))
            {
                // If the current 'on' mapping doesn't have this block yet, skip
                continue;
            }

            if (support == TABLE)
            {
                sb.Append($" ({block} --> OnTable)");
                current_states.Add(StatementTerm.from_string($"({block} --> OnTable)"));
            }
            else
            {
                sb.Append($" ((*,{ block}, { support}) --> On)");
                current_states.Add(StatementTerm.from_string($"((*,{block}, {support}) --> On)"));
            }
        }

        //// Clear info for EVERY block (if you want it, same style as goal)
        foreach (var kvp in blocks)
        {
            string block = kvp.Key;

            if (clear.Contains(block))
            {
                current_states.Add(StatementTerm.from_string($"({block} --> Clear)"));
            }
        }
        str = sb.ToString();
        return current_states;
    }

    #endregion
    private void CheckGoalReached()
    {
        if (goalReached) return;   // already in goal, don't re-do

        // Compare current "on" mapping with the goal "on" mapping.
        // If every block's support matches, we've reached the goal.
        int matches = CountMatchingOn(on, goalOn);
        if (matches == blocks.Count)   // or numberOfBlocks
        {
            goalReached = true;
            Debug.Log("Goal state reached!");

            SetAllBlocksColor(Color.green);
        }
    }

    private void SetAllBlocksColor(Color color)
    {
        foreach (var kvp in blocks)
        {
            var image = kvp.Value.instance.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }
        }
    }


    #endregion

    #region Utility

    /// <summary>
    /// Fisher–Yates shuffle for lists.
    /// </summary>
    private void Shuffle<T>(IList<T> list)
    {
        int n = list.Count;
        for (int i = 0; i < n - 1; i++)
        {
            int j = Random.Range(i, n);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    #endregion
}
