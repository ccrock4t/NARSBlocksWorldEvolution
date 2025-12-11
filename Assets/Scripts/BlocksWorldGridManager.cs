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
    private int rows = 3;
    private int cols = 3;

    // size and spacing of each mini-world
    [SerializeField] private Vector2 cellSize = new Vector2(300f, 300f);
    [SerializeField] private Vector2 cellSpacing = new Vector2(20f, 20f);


    private string _csvPath;
    private StreamWriter _csv;

    private int fixedUpdatesPerSimulationTick = 3;

    private int _fixedUpdateCounter = 0;

    public static int episode_length = 40;
    public const int NUM_OF_NARS_AGENTS = 25;
    AnimatTable hallOfFameTable;
    AnimatTable recentTable;

    [SerializeField] TextMeshProUGUI TimestepTxt;
    [SerializeField] TextMeshProUGUI HighScoreTxt;
    [SerializeField] TextMeshProUGUI GenerationTxt;
    [SerializeField] TextMeshProUGUI EpisodeTxt;
    float high_score;


    int episode_counter = 0;
    int generation_counter = 0;
    int EPISODES_PER_GENERATION = 3;

    public class Agent
    {
        public NARSGenome genome;
        public NARS nars;
        public NARSBody narsBody;

        public Agent(NARSGenome gene)
        {
            if(gene == null)
            {
                genome = new NARSGenome();
            }
            else
            {
                genome = gene;
            }
            nars = new NARS(genome);
            narsBody = new(nars);
        }

        public void Reset()
        {
            nars = new NARS(genome);
            narsBody.ResetForEpisode(nars);
        }

    }

    public class BlocksWorldInstance
    {
        public BlocksWorld blocksworld;
        public Agent agent;


        public BlocksWorldInstance(BlocksWorld blocksworld, NARSGenome gene)
        {
            this.agent = new Agent(gene);
            this.blocksworld = blocksworld;
        }

        public void ResetAgent()
        {
            this.agent.Reset();
        }
    }

    public List<BlocksWorldInstance> population = new();
    private int timestep;

    private void Start()
    {
        hallOfFameTable = new(AnimatTable.SortingRule.sorted, AnimatTable.ScoreType.objective_fitness);
        recentTable = new(AnimatTable.SortingRule.unsorted, AnimatTable.ScoreType.objective_fitness);
        SpawnNewGeneration(true);
        SpawnNewEpisode();
        InitCsv();
    }


    public void SpawnNewEpisode()
    {
        int i = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var worldInstance = population[i++];
                // instantiate one BlocksWorld
                BlocksWorld world = Instantiate(worldPrefab, parent).GetComponent<BlocksWorld>();
                world.Initialize();
                // position its root RectTransform in the grid
                RectTransform rt = world.GetComponent<RectTransform>();

                float x = c * (cellSize.x + cellSpacing.x);
                float y = -r * (cellSize.y + cellSpacing.y); // negative to go down

                rt.sizeDelta = cellSize;
                rt.anchoredPosition = new Vector2(x, y);


                worldInstance.agent.genome.SetIdealGoal(world);
                worldInstance.blocksworld = world;
            }
        }
    }

    public void SpawnNewGeneration(bool initial)
    {
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {

                NARSGenome genome;
                if (initial)
                {
                    genome = new();
                }
                else
                {
                    int sexual = UnityEngine.Random.Range(0, 2);
                    int table = UnityEngine.Random.Range(0, 2);
                    NARSGenome[] new_genomes;
                    if (table == 0)
                    {
                        new_genomes = hallOfFameTable.GetNewAnimatReproducedFromTable(sexual == 1);
                    }
                    else
                    {
                        new_genomes = recentTable.GetNewAnimatReproducedFromTable(sexual == 1);
                    }
                    
                    genome = new_genomes[0];
                }

                BlocksWorldInstance worldInstance = new(null, genome);
                population.Add(worldInstance);
            }
        }
    }

    public void FinishEpisode()
    {
        foreach (var instance in population)
        {
            var agent_fitness = instance.agent.narsBody.GetEpisodeFitness();
            var world_fitness = instance.blocksworld.GetFitnessForWorldState();
            float fitness = agent_fitness * world_fitness;
            instance.agent.narsBody.total_fitness += fitness;
            Destroy(instance.blocksworld.gameObject);
            instance.ResetAgent();

        }
        WriteCsvRow();
    }

    public void FinishGeneration()
    {
        foreach(var instance in population)
        {
            if(instance.agent.narsBody.total_fitness > 0)
            {
                int test = 1;
            }
            float fitness = instance.agent.narsBody.total_fitness / (float)EPISODES_PER_GENERATION;
            high_score = math.max(fitness, high_score);
            hallOfFameTable.TryAdd(fitness, instance.agent.genome);
            recentTable.TryAdd(fitness, instance.agent.genome);
            Destroy(instance.blocksworld.gameObject);
        }
        population.Clear();
    }


    void FixedUpdate()
    {
        _fixedUpdateCounter++;

        if (_fixedUpdateCounter >= fixedUpdatesPerSimulationTick)
        {
            _fixedUpdateCounter = 0;   // reset for next tick
            timestep++;
            UpdateUI();
  
            if (timestep < episode_length)
            {
                StepSimulation();
            }
            else
            {
                episode_counter++;
                FinishEpisode();
                if (episode_counter >= EPISODES_PER_GENERATION)
                {
                    FinishGeneration();
                    GC.Collect();
                    SpawnNewGeneration(false);
                    episode_counter = 0;
                    generation_counter++;
                }
             
                SpawnNewEpisode();
                
            
                timestep = 0;
            }
            // WriteCsvRow();   // still runs on each tick
        }
        if(generation_counter == 52)
        {
            Application.Quit();
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
                for(int j =0; j< agent.nars.genome.goals.Count; j++)
                {
                    var goal_data1 = agent.nars.genome.goals[j];
                    var goal = new Goal(agent.nars, goal_data1.statement, goal_data1.evidence, occurrence_time: agent.nars.current_cycle_number);
                    agent.nars.SendInput(goal);
                    for (int k = j; k < agent.nars.genome.goals.Count; k++)
                    {
                        var goal_data2 = agent.nars.genome.goals[k];
                        var compound_statement = TermHelperFunctions.TryGetCompoundTerm(new() { goal_data1.statement, goal_data2.statement }, TermConnector.ParallelConjunction);
                        var goal2 = new Goal(agent.nars, compound_statement, new(1.0f, 0.99f), occurrence_time: agent.nars.current_cycle_number);
                        agent.nars.SendInput(goal2);
                    }
                }

                agent.nars.do_working_cycle();
            }
            agent.narsBody.MotorAct(blocksworld);

            agent.narsBody.timesteps_alive++;
            // agent.narsBody.remaining_life--;
            blocksworld.GetCurrentState(out var stateStr);
            agent.narsBody.AddUniqueStateReached(stateStr);


        }
    }


    void WriteCsvRow()
    {
        if (_csv == null) return;

        // max hallOfFameTable score
        float maxTable = 0f;
        var best = hallOfFameTable.GetBest();
        if (best.HasValue) maxTable = best.Value.score;

        // mean (average) and median
        int count = hallOfFameTable.Count();
        float mean = (count > 0) ? (hallOfFameTable.total_score / count) : 0f;
       // float median = GetMedianTableScore();

        string line = string.Join(",",
            maxTable.ToString(CultureInfo.InvariantCulture),
            mean.ToString(CultureInfo.InvariantCulture)//,
            //median.ToString(CultureInfo.InvariantCulture)
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
        _csv.WriteLine("max_table_score,mean_table_score");//,median_table_score");

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
        GenerationTxt.text = "Generation: " + generation_counter;
        EpisodeTxt.text = "Episode: " + episode_counter;
        // timestepTXT.text = "Timestep: " + timestep;
        // scoreTXT.text = "High Score: " + high_score;
        // genomeSizeTXT.text = "Largest genome: " + largest_genome;
    }


}
