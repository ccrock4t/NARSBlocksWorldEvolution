using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

public class BlocksWorldGridManager : MonoBehaviour
{
    [SerializeField] private RectTransform parent;   // UI container under main Canvas
    [SerializeField] private GameObject worldPrefab;   // UI container under main Canvas
    [SerializeField] private int rows = 5;
    [SerializeField] private int cols = 5;

    // size and spacing of each mini-world
    [SerializeField] private Vector2 cellSize = new Vector2(300f, 300f);
    [SerializeField] private Vector2 cellSpacing = new Vector2(20f, 20f);


    private string _csvPath;
    private StreamWriter _csv;

    private int updatesPerTick = 5;

    private int _fixedUpdateCounter = 0;


    public const int NUM_OF_NARS_AGENTS = 25;
    AnimatTable table;

    public class Agent
    {
        public NARSGenome genome;
        public NARS nars;
        public NARSBody narsBody;

        public Agent(BlocksWorld blocksworld, NARSGenome gene)
        {
            if(gene == null)
            {
                genome = new NARSGenome();
            }
            else
            {
                genome = gene;
            }

             genome.SetIdealGoal(blocksworld);
            nars = new NARS(genome);
            narsBody = new(nars);
        }

    }

    public class BlocksWorldInstance
    {
        public BlocksWorld blocksworld;
        public Agent agent;

        public BlocksWorldInstance(BlocksWorld blocksworld, NARSGenome gene)
        {
            this.agent = new Agent(blocksworld, gene);
            this.blocksworld = blocksworld;
        }
    }

    public List<BlocksWorldInstance> population = new();
    private int timestep;

    private void Start()
    {
        table = new(AnimatTable.SortingRule.sorted, AnimatTable.ScoreType.objective_fitness);
        SpawnGeneration(true);
    }

    public void SpawnGeneration(bool initial)
    {
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {

                // instantiate one BlocksWorld
                BlocksWorld world = Instantiate(worldPrefab, parent).GetComponent<BlocksWorld>();
                world.Initialize();
                // position its root RectTransform in the grid
                RectTransform rt = world.GetComponent<RectTransform>();

                float x = c * (cellSize.x + cellSpacing.x);
                float y = -r * (cellSize.y + cellSpacing.y); // negative to go down

                rt.sizeDelta = cellSize;
                rt.anchoredPosition = new Vector2(x, y);

                // OPTIONAL: tweak per-world parameters
                // world.numberOfBlocks = 3;  // if you expose it as public or property

                NARSGenome genome;
                if (initial)
                {
                    genome = new();
                }
                else
                {
                    int sexual = UnityEngine.Random.Range(0, 2);
                    NARSGenome[] new_genomes = table.GetNewAnimatReproducedFromTable(sexual == 1);
                    genome = new_genomes[0];
                }

                BlocksWorldInstance worldInstance = new(world, genome);
                population.Add(worldInstance);
            }
        }
    }

    public void FinishGeneration()
    {
        foreach(var instance in population)
        {
            float fitness = instance.agent.narsBody.GetFitness();
            high_score = math.max(fitness, high_score);
            table.TryAdd(fitness, instance.agent.genome);
            Destroy(instance.blocksworld.gameObject);
        }
        population.Clear();
    }

    [SerializeField] TextMeshProUGUI TimestepTxt;
    [SerializeField] TextMeshProUGUI HighScoreTxt;
    float high_score;

    void FixedUpdate()
    {
        _fixedUpdateCounter++;

        if (_fixedUpdateCounter >= updatesPerTick)
        {
            _fixedUpdateCounter = 0;   // reset for next tick
            timestep++;
            UpdateUI();
  
            if (timestep < 10)
            {
                StepSimulation();
            }
            else
            {
                FinishGeneration();
                SpawnGeneration(false);
                timestep = 0;
            }
            // WriteCsvRow();   // still runs on each tick
        }
    }


    void StepSimulation()
    {
        // Collect current positions of all animals (wolves + goats).

        foreach (var blocksWorldInstance in population)
        {
            var agent = blocksWorldInstance.agent;
            var blocksworld = blocksWorldInstance.blocksworld;
 
            for (int i = 0; i < 4; i++)
            {
                agent.narsBody.Sense(blocksworld);
                // enter instinctual goals
                foreach (var goal_data in agent.nars.genome.goals)
                {
                    var goal = new Goal(agent.nars, goal_data.statement, goal_data.evidence, occurrence_time: agent.nars.current_cycle_number);
                    agent.nars.SendInput(goal);
                }
                agent.nars.do_working_cycle();
            }
            agent.narsBody.MotorAct(blocksworld);

            agent.narsBody.timesteps_alive++;
            // agent.narsBody.remaining_life--;

            
        }
    }


    void WriteCsvRow()
    {
        if (_csv == null) return;

        // max table score
        float maxTable = 0f;
        var best = table.GetBest();
        if (best.HasValue) maxTable = best.Value.score;

        // mean (average) and median
        int count = table.Count();
        float mean = (count > 0) ? (table.total_score / count) : 0f;
        float median = GetMedianTableScore();

        string line = string.Join(",",
            maxTable.ToString(CultureInfo.InvariantCulture),
            mean.ToString(CultureInfo.InvariantCulture),
            median.ToString(CultureInfo.InvariantCulture)
        );

        _csv.WriteLine(line);
        _csv.Flush();
    }

    private float GetMedianTableScore()
    {
        throw new NotImplementedException();
    }

    void InitCsv()
    {
        var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var root = GetLogRootDirectory();


        _csvPath = Path.Combine(root, $"stats_{stamp}.csv");
        _csv = new StreamWriter(_csvPath, false);
        _csv.WriteLine("max_table_score,mean_table_score,median_table_score");

        _csv.Flush();

        Debug.Log($"CSV logging to: {_csvPath}");
    }

    string GetLogRootDirectory()
    {
#if UNITY_EDITOR
        // Editor: project root (one level up from Assets)
        return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
#elif UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX
    // Standalone Win/Linux: parent of "<Game>_Data"
    return Directory.GetParent(Application.dataPath).FullName;
#elif UNITY_STANDALONE_OSX
    // macOS: Application.dataPath = ".../MyGame.app/Contents"
    // Install dir = parent of the .app bundle
    return Directory.GetParent(Application.dataPath).Parent.Parent.FullName;
#else
    // Mobile, WebGL, consoles, etc. — use the safe location
    return Application.persistentDataPath;
#endif
    }

    void OnDestroy()
    {
        if (_csv != null)
        {
            _csv.Flush();
            _csv.Dispose();
            _csv = null;
        }
    }

    void OnApplicationQuit()
    {
        OnDestroy(); // ensure it’s closed on quit, too
    }

    public void UpdateUI()
    {
        TimestepTxt.text = "Timestep: " + timestep;
        HighScoreTxt.text = "High Score: " + high_score;
        // timestepTXT.text = "Timestep: " + timestep;
        // scoreTXT.text = "High Score: " + high_score;
        // genomeSizeTXT.text = "Largest genome: " + largest_genome;
    }


}
