
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using static MutationHelpers;
using static Unity.Burst.Intrinsics.X86.Avx;
using Random = UnityEngine.Random;
public class NARSGenome
{
    const float CHANCE_TO_MUTATE_BELIEF_CONTENT = 0.8f;
    const float CHANCE_TO_MUTATE_TRUTH_VALUES = 0.8f;
    const float CHANCE_TO_MUTATE_PERSONALITY_PARAMETERS = 0.8f;
    const float CHANCE_TO_MUTATE_BELIEFS = 0.8f;

    const bool ALLOW_VARIABLES = true;
    const bool ALLOW_COMPOUNDS = true;
    public static bool USE_GENERALIZATION = false;

    public bool LIMIT_SIZE = false;
    public int SIZE_LIMIT = 10;



    public enum NARS_Evolution_Type
    {
        NARS_NO_CONTINGENCY_FIXED_PERSONALITY_LEARNING,
        NARS_NO_CONTINGENCY_RANDOM_PERSONALITY_LEARNING,

        NARS_EVOLVE_CONTINGENCIES_FIXED_PERSONALITY_NO_LEARNING,
        NARS_EVOLVE_CONTINGENCIES_RANDOM_PERSONALITY_NO_LEARNING,

        NARS_EVOLVE_CONTINGENCIES_FIXED_PERSONALITY_LEARNING,
        NARS_EVOLVE_CONTINGENCIES_RANDOM_PERSONALITY_LEARNING,

        NARS_EVOLVE_PERSONALITY_LEARNING,
        NARS_EVOLVE_PERSONALITY_AND_CONTINGENCIES_LEARNING,

        NARS_EVOLVE_PERSONALITY_AND_CONTINGENCIES_NO_LEARNING
    }

    public static NARS_Evolution_Type NARS_EVOLVE_TYPE = NARS_Evolution_Type.NARS_EVOLVE_PERSONALITY_AND_CONTINGENCIES_NO_LEARNING;


    public static bool RANDOM_PERSONALITY()
    {
        return NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_NO_CONTINGENCY_RANDOM_PERSONALITY_LEARNING
            || NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_CONTINGENCIES_RANDOM_PERSONALITY_NO_LEARNING
            || NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_CONTINGENCIES_RANDOM_PERSONALITY_LEARNING;
    }

    public static bool USE_LEARNING()
    {
        return NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_NO_CONTINGENCY_FIXED_PERSONALITY_LEARNING
            || NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_NO_CONTINGENCY_RANDOM_PERSONALITY_LEARNING
            || NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_CONTINGENCIES_FIXED_PERSONALITY_LEARNING
            || NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_CONTINGENCIES_RANDOM_PERSONALITY_LEARNING
            || NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_PERSONALITY_LEARNING
            || NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_PERSONALITY_AND_CONTINGENCIES_LEARNING;
    }

    public static bool EVOLVE_PERSONALITY()
    {
        return NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_PERSONALITY_LEARNING
            || NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_PERSONALITY_AND_CONTINGENCIES_LEARNING
            || NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_PERSONALITY_AND_CONTINGENCIES_NO_LEARNING;
    }

    public static bool USE_AND_EVOLVE_CONTINGENCIES()
    {
        return NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_CONTINGENCIES_FIXED_PERSONALITY_NO_LEARNING
            || NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_CONTINGENCIES_RANDOM_PERSONALITY_NO_LEARNING
            || NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_CONTINGENCIES_FIXED_PERSONALITY_LEARNING
            || NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_CONTINGENCIES_RANDOM_PERSONALITY_LEARNING
            || NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_PERSONALITY_AND_CONTINGENCIES_LEARNING
            || NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_PERSONALITY_AND_CONTINGENCIES_NO_LEARNING;
    }

    public struct EvolvableSentence
    {
        public StatementTerm statement;
        public EvidentialValue evidence;

        public EvolvableSentence(StatementTerm statement, float2 evidence)
        {
            this.statement = statement;
            this.evidence = new EvidentialValue(evidence.x, evidence.y);
        }
    }

    public static bool sensorymotor_statements_initialized = false;
    //public static Dictionary<Direction, StatementTerm> move_op_terms = new();
    //public static Dictionary<Direction, StatementTerm> eat_op_terms = new();

    //public static Dictionary<Direction, StatementTerm> grass_seen_terms = new();
    //public static Dictionary<Direction, StatementTerm> goat_seen_terms = new();
    //public static Dictionary<Direction, StatementTerm> water_seen = new();s


    public static List<StatementTerm> SENSORY_TERM_SET = new();
    public static List<StatementTerm> MOTOR_TERM_SET = new();

    public Dictionary<string, bool> belief_statement_strings = new();
    public List<EvolvableSentence> beliefs;
    public List<EvolvableSentence> goals;


    public struct PersonalityParameters
    {
        public float k;
        public float T;
        public int Anticipation_Window;
        public float Forgetting_Rate;
        public int Event_Buffer_Capacity;
        public int Table_Capacity;
        public int Evidential_Base_Length;
        public float Time_Projection_Event;
        public float Time_Projection_Goal;

        public float Compound_Confidence;



        public int RuntimeCompounds1;
        public int RuntimeCompounds2;
        public int RuntimeCompounds3;


        public float Get(int i)
        {
            if (i == 0) return k;
            else if (i == 1) return T;
            else if (i == 2) return Anticipation_Window;
            else if (i == 3) return Forgetting_Rate;
            else if (i == 4) return Event_Buffer_Capacity;
            else if (i == 5) return Table_Capacity;
            else if (i == 6) return Evidential_Base_Length;
            else if (i == 7) return Time_Projection_Event;
            else if (i == 8) return Time_Projection_Goal;
            else if (i == 9) return Compound_Confidence;
            else if (i == 10) return RuntimeCompounds1;
            else if (i == 11) return RuntimeCompounds2;
            else if (i == 12) return RuntimeCompounds3;
            else Debug.LogError("error");
            return -1;
        }

        public static string GetName(int i)
        {
            if (i == 0) return "k";
            else if (i == 1) return "T";
            else if (i == 2) return "Anticipation_Window";
            else if (i == 3) return "Forgetting_Rate";
            else if (i == 4) return "Event_Buffer_Capacity";
            else if (i == 5) return "Table_Capacity";
            else if (i == 6) return "Evidential_Base_Length";
            else if (i == 7) return "Time_Projection_Event";
            else if (i == 8) return "Time_Projection_Goal";
            else if (i == 9) return "Compound_Confidence";
            else if (i == 10) return "RuntimeCompounds1";
            else if (i == 11) return "RuntimeCompounds2";
            else if (i == 12) return "RuntimeCompounds3";
            else Debug.LogError("error");
            return "";
        }


        public void Set(int i, float value)
        {
            if (i == 0) k = (float)value;
            else if (i == 1) T = (float)value;
            else if (i == 2) Anticipation_Window = (int)value;
            else if (i == 3) Forgetting_Rate = (float)value;
            else if (i == 4) Event_Buffer_Capacity = (int)value;
            else if (i == 5) Table_Capacity = (int)value;
            else if (i == 6) Evidential_Base_Length = (int)value;
            else if (i == 7) Time_Projection_Event = (float)value;
            else if (i == 8) Time_Projection_Goal = (float)value;
            else if (i == 9) Compound_Confidence = (float)value;
            else if (i == 10) RuntimeCompounds1 = (int)value;
            else if (i == 11) RuntimeCompounds2 = (int)value;
            else if (i == 12) RuntimeCompounds3 = (int)value;
            else Debug.LogError("error");
        }

        public static int GetParameterCount()
        {
            return 13;
        }
    }

    public PersonalityParameters personality_parameters;


    const int MAX_INITIAL_BELIEFS = 5;



    public NARSGenome(List<EvolvableSentence> beliefs_to_clone = null,
        List<EvolvableSentence> goals_to_clone = null,
        PersonalityParameters? personality_to_clone = null
        )
    {
        if (!sensorymotor_statements_initialized)
        {
            for (int i = 0; i < BlocksWorld.numberOfBlocks; i++)
            {
                var blockName1 = BlocksWorld.GetBlockName(i);

                SENSORY_TERM_SET.Add((StatementTerm)Term.from_string($"({blockName1} --> Clear)"));
                SENSORY_TERM_SET.Add((StatementTerm)Term.from_string($"({blockName1} --> OnTable)"));
                MOTOR_TERM_SET.Add((StatementTerm)Term.from_string("((*,{SELF}," + blockName1 + ") --> UNSTACK)"));
                for (int j = 0; j < BlocksWorld.numberOfBlocks; j++)
                {
                    if (i <= j) continue;
                    var blockName2 = BlocksWorld.GetBlockName(j);

                    SENSORY_TERM_SET.Add((StatementTerm)Term.from_string($"((*,{blockName1},{blockName2}) --> On)"));
                    SENSORY_TERM_SET.Add((StatementTerm)Term.from_string($"((*,{blockName2},{blockName1}) --> On)"));

                    MOTOR_TERM_SET.Add((StatementTerm)Term.from_string("((*,{SELF}," + blockName1 + "," + blockName2 + ") --> STACK)"));
                    MOTOR_TERM_SET.Add((StatementTerm)Term.from_string("((*,{SELF}," + blockName2 + "," + blockName1 + ") --> STACK)"));
                }
            }
            sensorymotor_statements_initialized = true;
        }

        goals = new();
        beliefs = new();

        if (USE_AND_EVOLVE_CONTINGENCIES())
        {
            if (beliefs_to_clone == null)
            {

                int rnd_amt = UnityEngine.Random.Range(1, MAX_INITIAL_BELIEFS);
                for (int i = 0; i < rnd_amt; i++)
                {
                    AddNewRandomBelief();
                }
            }
            else
            {
                foreach (var belief in beliefs_to_clone)
                {
                    AddNewBelief(belief);
                }
            }
        }

        this.personality_parameters = new();
        if (personality_to_clone != null)
        {
            this.personality_parameters = (PersonalityParameters)personality_to_clone;

        }
        else
        {
            if (EVOLVE_PERSONALITY())
            {
                RandomizePersonalityParameters(ref this.personality_parameters);
            }
            else
            {
                this.personality_parameters = DefaultParameters();
            }
        }

        if (NARSGenome.RANDOM_PERSONALITY())
        {
            RandomizePersonalityParameters(ref this.personality_parameters);
        }
    }

    public static PersonalityParameters DefaultParameters()
    {
        PersonalityParameters personality_parameters = new();
        // k
        personality_parameters.k = 1;

        // T
        personality_parameters.T = 0.51f;

        // Anticipation window
        personality_parameters.Anticipation_Window = 5;

        // Forgetting rate
        personality_parameters.Forgetting_Rate = 10;

        // Event buffer capacity
        personality_parameters.Event_Buffer_Capacity = 10;

        // Table capacity
        personality_parameters.Table_Capacity = 5;

        // Evidential base length
        personality_parameters.Evidential_Base_Length = 20;

        // Time Projection Event
        personality_parameters.Time_Projection_Event = 10;

        // Time ProjectionGoal
        personality_parameters.Time_Projection_Goal = 1;

        // Generalization Confidence
        personality_parameters.Compound_Confidence = 0.99f;


        return personality_parameters;
    }

    public static void RandomizePersonalityParameters(ref PersonalityParameters personality_parameters)
    {
        // k
        var kRange = GetKRange();
        personality_parameters.k = UnityEngine.Random.Range(kRange.x, kRange.y);

        // T
        var tRange = GetTRange();
        personality_parameters.T = UnityEngine.Random.Range(tRange.x, tRange.y);

        // Anticipation window
        var awRange = GetAnticipationWindowRange();
        personality_parameters.Anticipation_Window = UnityEngine.Random.Range(awRange.x, awRange.y + 1);

        // Forgetting rate
        var frRange = GetForgettingRateRange();
        personality_parameters.Forgetting_Rate = UnityEngine.Random.Range(frRange.x, frRange.y);

        // Event buffer capacity
        var ebcRange = GetEventBufferCapacityRange();
        personality_parameters.Event_Buffer_Capacity = UnityEngine.Random.Range(ebcRange.x, ebcRange.y + 1);

        // Table capacity
        var tcRange = GetTableCapacityRange();
        personality_parameters.Table_Capacity = UnityEngine.Random.Range(tcRange.x, tcRange.y + 1);

        // Evidential base length
        var eblRange = GetEvidentialBaseLengthRange();
        personality_parameters.Evidential_Base_Length = UnityEngine.Random.Range(eblRange.x, eblRange.y + 1);

        // Time Projection Event
        var timeProjectionEventRange = GetTimeProjectionEventRange();
        personality_parameters.Time_Projection_Event = UnityEngine.Random.Range(timeProjectionEventRange.x, timeProjectionEventRange.y);

        // Time ProjectionGoal
        var timeProjectionGoalRange = GetTimeProjectionGoalRange();
        personality_parameters.Time_Projection_Goal = UnityEngine.Random.Range(timeProjectionGoalRange.x, timeProjectionGoalRange.y);

        // Compound Confidence
        var compoundConfidenceRange = GetCompoundConfidenceRange();
        personality_parameters.Compound_Confidence = UnityEngine.Random.Range(compoundConfidenceRange.x, compoundConfidenceRange.y);


        // Runtime compounds
        var runtimeCompoundRange = GetRuntimeCompoundRange();
        personality_parameters.RuntimeCompounds1 = UnityEngine.Random.Range(runtimeCompoundRange.x, runtimeCompoundRange.y + 1);
        personality_parameters.RuntimeCompounds2 = UnityEngine.Random.Range(runtimeCompoundRange.x, runtimeCompoundRange.y + 1);
        personality_parameters.RuntimeCompounds3 = UnityEngine.Random.Range(runtimeCompoundRange.x, runtimeCompoundRange.y + 1);
    }



    public static void AddEvolvableSentences(List<EvolvableSentence> list, (StatementTerm, float?, float?)[] statement_strings)
    {

        foreach (var statement in statement_strings)
        {
            float f = statement.Item2 == null ? 1.0f : (float)statement.Item2;
            float c = statement.Item3 == null ? 0.99f : (float)statement.Item3;
            EvolvableSentence sentence = new(statement: statement.Item1,
                new float2(f, c));
            list.Add(sentence);
        }

    }


    // create <S &/ ^M =/> P>
    public static StatementTerm CreateContingencyStatement(Term S, Term M, Term P)
    {
        if (S == null) return (StatementTerm)Term.from_string("(" + M.ToString() + " =/> " + P.ToString() + ")");
        return (StatementTerm)Term.from_string("((&/," + S.ToString() + "," + M.ToString() + ") =/> " + P.ToString() + ")");
    }


    public NARSGenome Clone()
    {
        NARSGenome cloned_genome = new(
            beliefs,
            goals,
            this.personality_parameters);

        return cloned_genome;
    }

    private static Vector2 GetKRange() => new(1f, 10f);
    private static Vector2 GetTRange() => new(0.51f, 1f);
    private static Vector2 GetTimeProjectionEventRange() => new(0.0000001f, 10f);
    private static Vector2 GetTimeProjectionGoalRange() => new(0.0000001f, 10f);
    private static Vector2 GetCompoundConfidenceRange() => new(0.0000001f, 0.99999f);
    private static Vector2 GetForgettingRateRange() => new(1, 250f);
    private static Vector2Int GetAnticipationWindowRange() => new(1, 30);
    private static Vector2Int GetEventBufferCapacityRange() => new(3, 30);
    private static Vector2Int GetTableCapacityRange() => new(1, 20);
    private static Vector2Int GetEvidentialBaseLengthRange() => new(1, 50);

    private static Vector2Int GetRuntimeCompoundRange() => new(0, 1);

    // Local helpers
    void MutateFloat(ref float field, Vector2 range, float replaceChance, float mutate_chance)
    {
        if (UnityEngine.Random.value < (1f - mutate_chance)) return; // certain chance to mutate
        if (UnityEngine.Random.value < replaceChance)
        {
            field = UnityEngine.Random.Range(range.x, range.y);
        }
        else
        {
            field += (float)GetPerturbationFromRange(range);
        }
        field = math.clamp(field, range.x, range.y);
    }

    void MutateInt(ref int field, Vector2Int range, float replaceChance, float mutate_chance)
    {
        if (UnityEngine.Random.value < (1f - mutate_chance)) return; // certain chance to mutate

        if (UnityEngine.Random.value < replaceChance)
        {
            // int Random.Range max is exclusive; add +1 to include range.y
            field = UnityEngine.Random.Range(range.x, range.y + 1);
        }
        else
        {
            field += (int)GetPerturbationFromRange(range);
        }
        field = (int)math.clamp(field, range.x, range.y);
    }

    void MutateBool(ref int field, Vector2Int range, float mutate_chance)
    {
        if (UnityEngine.Random.value < (1f - mutate_chance)) return; // certain chance to mutate

        // int Random.Range max is exclusive; add +1 to include range.y
        field = UnityEngine.Random.Range(range.x, range.y + 1);

        field = (int)math.clamp(field, range.x, range.y);
    }


    static Vector2 truth_range = new(0f, 1f);


    public void Mutate()
    {

        float rnd = 0;

        rnd = UnityEngine.Random.value;

        if (USE_AND_EVOLVE_CONTINGENCIES() && rnd < CHANCE_TO_MUTATE_BELIEFS)
        {
            MutateBeliefs();
        }

        rnd = UnityEngine.Random.value;

        if (EVOLVE_PERSONALITY() && rnd < CHANCE_TO_MUTATE_PERSONALITY_PARAMETERS)
        {
            MutatePersonalityParameters();
        }

    }

    public void MutateBeliefs()
    {
        float rnd = UnityEngine.Random.value;
        if (rnd < CHANCE_TO_MUTATE_BELIEF_CONTENT)
        {
            if (ALLOW_VARIABLES && !ALLOW_COMPOUNDS)
            {
                int r = UnityEngine.Random.Range(0, 100); // 0–99

                if (r < 25)
                {
                    AddNewRandomBelief();
                }
                else if (r < 50)
                {
                    RemoveRandomBelief();
                }
                else if (r < 75)
                {
                    ModifyRandomBelief();
                }
                else
                {

                    ToggleVariableRandomBelief();
                }
            }
            else if (ALLOW_COMPOUNDS && !ALLOW_VARIABLES)
            {
                int r = UnityEngine.Random.Range(0, 100); // 0–99

                if (LIMIT_SIZE && this.beliefs.Count >= SIZE_LIMIT && r < 25)
                {
                    r = UnityEngine.Random.Range(25, 100); // 0–99
                }

                if (r < 25)
                {
                    AddNewRandomBelief();
                }
                else if (r < 50)
                {
                    RemoveRandomBelief();
                }
                else if (r < 75)
                {
                    ModifyRandomBelief();
                }
                else
                {

                    MutateCompound();
                }
            }
            else if (ALLOW_COMPOUNDS && ALLOW_VARIABLES)
            {
                int r = UnityEngine.Random.Range(0, 100); // 0–99

                if (LIMIT_SIZE && this.beliefs.Count >= SIZE_LIMIT && r < 20)
                {
                    r = UnityEngine.Random.Range(20, 100); // 0–99
                }

                if (r < 20)
                {
                    AddNewRandomBelief();
                }
                else if (r < 40)
                {
                    RemoveRandomBelief();
                }
                else if (r < 60)
                {
                    ModifyRandomBelief();
                }
                else if (r < 80)
                {
                    ToggleVariableRandomBelief();
                }
                else
                {

                    MutateCompound();
                }
            }
            else
            {
                int r = UnityEngine.Random.Range(0, 100); // 0–99

                if (r < 33)
                {
                    AddNewRandomBelief();
                }
                else if (r < 66)
                {
                    RemoveRandomBelief();
                }
                else
                {
                    ModifyRandomBelief();
                }
            }


        }

        rnd = UnityEngine.Random.value;
        if (rnd < CHANCE_TO_MUTATE_TRUTH_VALUES)
        {
            const float CHANCE_TO_REPLACE_TRUTH_VALUE = 0.05f;
            const float CHANCE_TO_MUTATE = 0.5f;
            for (int i = 0; i < this.beliefs.Count; i++)
            {
                EvolvableSentence sentence = this.beliefs[i];
                // sentence.evidence.frequency = 1.0f;
                //sentence.evidence.confidence = 0.999f;
                MutateFloat(ref sentence.evidence.frequency, truth_range, CHANCE_TO_REPLACE_TRUTH_VALUE, CHANCE_TO_MUTATE);
                MutateFloat(ref sentence.evidence.confidence, truth_range, CHANCE_TO_REPLACE_TRUTH_VALUE, CHANCE_TO_MUTATE);
                sentence.evidence.confidence = math.clamp(sentence.evidence.confidence, 0.0001f, 0.9999f);
                this.beliefs[i] = sentence;
            }
        }
    }

    private void MutateCompound()
    {
        if (this.beliefs.Count == 0) return;
        int rnd_idx = UnityEngine.Random.Range(0, this.beliefs.Count);
        EvolvableSentence belief = this.beliefs[rnd_idx];

        string old_statement_string = belief.statement.ToString();

        // (S &/ ^M =/> P)
        StatementTerm implication = belief.statement;

        CompoundTerm subject = (CompoundTerm)implication.get_subject_term();
        Term S = (Term)subject.subterms[0];
        StatementTerm M = (StatementTerm)subject.subterms[1];
        Term P = (Term)implication.get_predicate_term();

        StatementTerm new_statement;


        int rnd_sensor = UnityEngine.Random.Range(0, 2);
        if (rnd_sensor == 0)
        {
            //S
            // compound S

            if (S is CompoundTerm sComp)
            {
                if (Random.value < 0.5 || sComp.subterms.Count == 3)
                {
                    //remove term
                    if (sComp.subterms.Count == 2)
                    {
                        // make compound into not compound
                        int rnd_subterm_idx = Random.Range(0, 2);
                        new_statement = CreateContingencyStatement(sComp.subterms[rnd_subterm_idx], M, P);
                    }
                    else// if (pComp.subterms.Count == 3)
                    {
                        // reduce compound from 3 to 2
                        int rnd_subterm_ignore = Random.Range(0, 3);
                        // make not compound into compound
                        List<Term> subterms = new();
                        int i = 0;
                        foreach (var st in sComp.subterms)
                        {
                            if (rnd_subterm_ignore != i)
                            {
                                subterms.Add(st);
                            }
                            i++;
                        }
                        CompoundTerm c = TermHelperFunctions.TryGetCompoundTerm(subterms, TermConnector.ParallelConjunction);
                        new_statement = CreateContingencyStatement(c, M, P);
                    }

                }
                else
                {
                    // add term to 2-compound
                    List<Term> subterms = new();
                    subterms.Add(sComp.subterms[0]);
                    subterms.Add(sComp.subterms[1]);
                    var ignoreList = new List<StatementTerm>();
                    foreach (var t in sComp.subterms)
                    {
                        if (t is StatementTerm st)
                            ignoreList.Add(st);
                    }

                    var randomS = GetRandomSensoryTerm(ignoreList);
                    subterms.Add(randomS);
                    CompoundTerm c = TermHelperFunctions.TryGetCompoundTerm(subterms, TermConnector.ParallelConjunction);
                    new_statement = CreateContingencyStatement(c, M, P);
                }

            }
            else if (S is StatementTerm)
            {
                var randomS = GetRandomSensoryTerm((StatementTerm)S);
                // make not compound into compound
                List<Term> subterms = new();
                subterms.Add(S);
                subterms.Add(randomS);
                CompoundTerm c = TermHelperFunctions.TryGetCompoundTerm(subterms, TermConnector.ParallelConjunction);
                new_statement = CreateContingencyStatement(c, M, P);
            }
            else
            {
                Debug.LogError("null");
                return;
            }
        }
        else if (rnd_sensor == 1)
        {
            // P
            // compound P

            if (P is CompoundTerm pComp)
            {
                if (Random.value < 0.5 || pComp.subterms.Count == 3)
                {
                    //remove term
                    if (pComp.subterms.Count == 2)
                    {
                        // make compound into not compound
                        int rnd_subterm_idx = Random.Range(0, 2);
                        new_statement = CreateContingencyStatement(S, M, pComp.subterms[rnd_subterm_idx]);
                    }
                    else// if (pComp.subterms.Count == 3)
                    {
                        // reduce compound from 3 to 2
                        int rnd_subterm_ignore = Random.Range(0, 3);


                        // make not compound into compound
                        List<Term> subterms = new();
                        int i = 0;
                        foreach (var st in pComp.subterms)
                        {
                            if (rnd_subterm_ignore != i)
                            {
                                subterms.Add(st);
                            }
                            i++;
                        }

                        CompoundTerm c = TermHelperFunctions.TryGetCompoundTerm(subterms, TermConnector.ParallelConjunction);
                        new_statement = CreateContingencyStatement(S, M, c);
                    }

                }
                else
                {
                    // add term to 2-compound
                    List<Term> subterms = new();
                    subterms.Add(pComp.subterms[0]);
                    subterms.Add(pComp.subterms[1]);
                    var ignoreList = new List<StatementTerm>();
                    foreach (var t in pComp.subterms)
                    {
                        if (t is StatementTerm st)
                            ignoreList.Add(st);
                    }

                    var randomP = GetRandomSensoryTerm(ignoreList);
                    subterms.Add(randomP);
                    CompoundTerm c = TermHelperFunctions.TryGetCompoundTerm(subterms, TermConnector.ParallelConjunction);
                    new_statement = CreateContingencyStatement(S, M, c);
                }

            }
            else if (P is StatementTerm)
            {
                var randomP = GetRandomSensoryTerm((StatementTerm)P);
                // make not compound into compound
                List<Term> subterms = new();
                subterms.Add(P);
                subterms.Add(randomP);
                CompoundTerm c = TermHelperFunctions.TryGetCompoundTerm(subterms, TermConnector.ParallelConjunction);
                new_statement = CreateContingencyStatement(S, M, c);
            }
            else
            {
                Debug.LogError("null");
                return;
            }
        }
        else
        {
            Debug.LogError("null");
            return;
        }

        string newStr = new_statement.ToString();
        if (belief_statement_strings.ContainsKey(newStr))
            return;

        belief.statement = new_statement;

        belief_statement_strings.Remove(old_statement_string);
        belief_statement_strings.Add(newStr, true);
        this.beliefs[rnd_idx] = belief;

    }


    private void ToggleVariableRandomBelief()
    {
        if (this.beliefs.Count == 0) return;
        int rnd_idx = UnityEngine.Random.Range(0, this.beliefs.Count);
        EvolvableSentence belief = this.beliefs[rnd_idx];
        CompoundTerm SandM = (CompoundTerm)belief.statement.get_subject_term();
        StatementTerm testS = (StatementTerm)SandM.subterms[0];
        int cnt = 0;
        while (!(testS.term_string.Contains("#x") || testS.term_string.Contains("block")))
        {
            rnd_idx = UnityEngine.Random.Range(0, this.beliefs.Count);
            belief = this.beliefs[rnd_idx];
            SandM = (CompoundTerm)belief.statement.get_subject_term();
            testS = (StatementTerm)SandM.subterms[0];
            cnt++;
            if (cnt > this.beliefs.Count)
            {
                break;
            }
        }

        if (cnt > this.beliefs.Count)
        {
            return;
        }

        string old_statement_string = belief.statement.ToString();

        // (S &/ ^M =/> P)
        StatementTerm implication = belief.statement;

        CompoundTerm subject = (CompoundTerm)implication.get_subject_term();
        Term P = implication.get_predicate_term();

        StatementTerm new_statement;

        Term S = (Term)subject.subterms[0];
        StatementTerm M = (StatementTerm)subject.subterms[1];

        bool contains_var = implication.contains_variable();

        Term new_S;
        Term new_M;
        Term new_P;
        if (contains_var)
        {
            //// turn from variable into concrete term
            CompoundTerm M_subject;
            if (M.subterms.Count == 2)
            {
                M_subject = (CompoundTerm)Term.from_string("(*,{SELF}," + GetRandomBlockName() + ")");
            }
            else if (M.subterms.Count == 3)
            {
                if (M.subterms[1].contains_variable())
                {
                    M_subject = (CompoundTerm)Term.from_string("(*,{SELF}," + GetRandomBlockName() + "," + M.subterms[2] + ")");
                }
                else if (M.subterms[2].contains_variable())
                {
                    M_subject = (CompoundTerm)Term.from_string("(*,{SELF}," + M.subterms[1] + "," + GetRandomBlockName() + ")");
                }
                else
                {
                    Debug.LogError("error");
                    return;
                }
               
            }
            else
            {
                Debug.LogError("error");
                return;
            }
            new_M = new StatementTerm(M_subject, M.get_predicate_term(), Copula.Inheritance);

            if (S is StatementTerm sStatement)
            {
                new_S = ConcretizeSensoryStatement(sStatement);
            }
            else if (S is CompoundTerm sCompound)
            {
                List<Term> new_S_subterms = new();
                for (int i = 0; i < sCompound.subterms.Count; i++)
                {
                    if (sCompound.subterms[i].contains_variable())
                    {
                        new_S_subterms.Add(ConcretizeSensoryStatement((StatementTerm)sCompound.subterms[i]));
                    }
                    else
                    {
                        new_S_subterms.Add(sCompound.subterms[i]);
                    }
                }

                new_S = TermHelperFunctions.TryGetCompoundTerm(new_S_subterms, (TermConnector)sCompound.connector);
            }
            else
            {
                Debug.LogError("error");
                return;
            }

            if (P is StatementTerm pStatement)
            {
                new_P = ConcretizeSensoryStatement(pStatement);
            }
            else if (P is CompoundTerm pCompound)
            {
                List<Term> new_P_subterms = new();
                for (int i = 0; i < pCompound.subterms.Count; i++)
                {
                    if (pCompound.subterms[i].contains_variable())
                    {
                        new_P_subterms.Add(ConcretizeSensoryStatement((StatementTerm)pCompound.subterms[i]));
                    }
                    else
                    {
                        new_P_subterms.Add(pCompound.subterms[i]);
                    }
                }

                new_P = TermHelperFunctions.TryGetCompoundTerm(new_P_subterms, (TermConnector)pCompound.connector);
            }
            else
            {
                Debug.LogError("error");
                return;
            }


        }
        else
        {
            //// turn from concrete term into variable
            CompoundTerm M_subject;
            if (M.subterms.Count == 2)
            {
                M_subject = (CompoundTerm)Term.from_string("(*,{SELF},#x)");
            }
            else if (M.subterms.Count == 3)
            {
                int RND = UnityEngine.Random.Range(0, 2);
                if (RND == 0)
                {
                    M_subject = (CompoundTerm)Term.from_string("(*,{SELF},#x," + M.subterms[2] + ")");
                }
                else if(RND == 1)
                {
                    M_subject = (CompoundTerm)Term.from_string("(*,{SELF}," + M.subterms[1] + ",#x)");
                }
                else
                {
                    Debug.LogError("error");
                    return;
                }
                
            }
            else
            {
                Debug.LogError("error");
                return;
            }

            new_M = new StatementTerm(M_subject, M.get_predicate_term(), Copula.Inheritance);


            if (S is StatementTerm sStatement)
            {
                new_S = VariabilizeSensoryStatement(sStatement);
            }
            else if (S is CompoundTerm sCompound)
            {
                // S contains multiple statements
                // select a random statement and variabilize it
                List<Term> new_S_subterms = new();
                int rnd_subterm_idx = UnityEngine.Random.Range(0, sCompound.subterms.Count);
                StatementTerm statement_to_variablize = (StatementTerm)sCompound.subterms[rnd_subterm_idx];
                StatementTerm variablized_statement = VariabilizeSensoryStatement(statement_to_variablize);

                for (int i = 0; i < sCompound.subterms.Count; i++)
                {
                    if(i == rnd_subterm_idx)
                    {
                        new_S_subterms.Add(variablized_statement);
                    }
                    else
                    {
                        new_S_subterms.Add(sCompound.subterms[i]);
                    }
                }

                new_S = TermHelperFunctions.TryGetCompoundTerm(new_S_subterms, (TermConnector)sCompound.connector);
            }
            else
            {
                Debug.LogError("error");
                return;
            }

            if (P is StatementTerm pStatement)
            {
                new_P = VariabilizeSensoryStatement(pStatement);
            }
            else if (P is CompoundTerm pCompound)
            {
                // P contains multiple statements
                // select a random statement and variabilize it
                List<Term> new_P_subterms = new();
                int rnd_subterm_idx = UnityEngine.Random.Range(0, pCompound.subterms.Count);
                StatementTerm statement_to_variablize = (StatementTerm)pCompound.subterms[rnd_subterm_idx];
                StatementTerm variablized_statement = VariabilizeSensoryStatement(statement_to_variablize);

                for (int i = 0; i < pCompound.subterms.Count; i++)
                {
                    if (i == rnd_subterm_idx)
                    {
                        new_P_subterms.Add(variablized_statement);
                    }
                    else
                    {
                        new_P_subterms.Add(pCompound.subterms[i]);
                    }
                }

                new_P = TermHelperFunctions.TryGetCompoundTerm(new_P_subterms, (TermConnector)pCompound.connector);
            }
            else
            {
                Debug.LogError("error");
                return;
            }
        }


        new_statement = CreateContingencyStatement(new_S, new_M, new_P);
        belief.statement = new_statement;
        string new_statement_string = new_statement.ToString();
        if (belief_statement_strings.ContainsKey(new_statement_string)) return;

        belief_statement_strings.Remove(old_statement_string);
        belief_statement_strings.Add(new_statement_string, true);

        this.beliefs[rnd_idx] = belief;
    }

    StatementTerm VariabilizeSensoryStatement(StatementTerm statement)
    {
        StatementTerm variabilized_statement;
        // S is a single statement
        if (statement.get_subject_term() is CompoundTerm c)
        {
            // e.g., <(*,A,B) --> On>
            int rnd_subterm_idx = UnityEngine.Random.Range(0, c.subterms.Count);
            List<Term> new_subterms = new();
            for(int i=0; i<c.subterms.Count; i++)
            {
                if(i == rnd_subterm_idx)
                {
                    new_subterms.Add(new VariableTerm("x", VariableTerm.VariableType.Independent));
                }
                else
                {
                    new_subterms.Add(c.subterms[i]);
                }
            }
            CompoundTerm new_subject = TermHelperFunctions.TryGetCompoundTerm(new_subterms, (TermConnector)c.connector);
            variabilized_statement = new StatementTerm(new_subject, statement.get_predicate_term(), Copula.Inheritance);
        }
        else if (statement.get_subject_term() is AtomicTerm)
        {
            variabilized_statement = new StatementTerm(new VariableTerm("x", VariableTerm.VariableType.Independent), statement.get_predicate_term(), Copula.Inheritance);
        }
        else
        {
            Debug.LogError("error");
            return null;
        }
        return variabilized_statement;
    }

    StatementTerm ConcretizeSensoryStatement(StatementTerm statement)
    {
        StatementTerm concretized_statement;
        // S is a single statement
        if (statement.get_subject_term() is CompoundTerm c)
        {
           // e.g., <(*,A,B) --> On>
            List<Term> new_subterms = new();
            for (int i = 0; i < c.subterms.Count; i++)
            {
                if (c.subterms[i].contains_variable())
                {
                    new_subterms.Add(Term.from_string(GetRandomBlockName()));
                }
                else
                {
                    new_subterms.Add(c.subterms[i]);
                }
            }
            CompoundTerm new_subject = TermHelperFunctions.TryGetCompoundTerm(new_subterms, (TermConnector)c.connector);
            concretized_statement = new StatementTerm(new_subject, statement.get_predicate_term(), Copula.Inheritance);
        }
        else if (statement.get_subject_term() is AtomicTerm)
        {
            concretized_statement = new StatementTerm(Term.from_string(GetRandomBlockName()), statement.get_predicate_term(), Copula.Inheritance);
        }
        else
        {
            Debug.LogError("error");
            return null;
        }
        return concretized_statement;
    }


    private string GetRandomBlockName()
    {
        int i = UnityEngine.Random.Range(0, BlocksWorld.numberOfBlocks);
        return BlocksWorld.GetBlockName(i);
    }

    public void MutatePersonalityParameters()
    {
        // tweakable: probability to *replace* a field instead of perturbing it
        const float CHANCE_TO_REPLACE_PARAM = 0.05f;
        const float CHANCE_TO_TOGGLE_BOOL = 0.1f;

        const float CHANCE_TO_MUTATE = 0.6f;

        // --- k ---
        var kRange = GetKRange();
        MutateFloat(ref this.personality_parameters.k, kRange, CHANCE_TO_REPLACE_PARAM, CHANCE_TO_MUTATE);

        // --- T ---
        var TRange = GetTRange();
        MutateFloat(ref this.personality_parameters.T, TRange, CHANCE_TO_REPLACE_PARAM, CHANCE_TO_MUTATE);

        // --- Anticipation window ---
        var AnticipationWindowRange = GetAnticipationWindowRange();
        MutateInt(ref this.personality_parameters.Anticipation_Window, AnticipationWindowRange, CHANCE_TO_REPLACE_PARAM, CHANCE_TO_MUTATE);

        // --- Forgetting rate ---
        var ForgettingRateRange = GetForgettingRateRange();
        MutateFloat(ref this.personality_parameters.Forgetting_Rate, ForgettingRateRange, CHANCE_TO_REPLACE_PARAM, CHANCE_TO_MUTATE);

        // --- Event buffer capacity ---
        var EventBufferCapacityRange = GetEventBufferCapacityRange();
        MutateInt(ref this.personality_parameters.Event_Buffer_Capacity, EventBufferCapacityRange, CHANCE_TO_REPLACE_PARAM, CHANCE_TO_MUTATE);

        // --- Table capacity ---
        var TableCapacityRange = GetTableCapacityRange();
        MutateInt(ref this.personality_parameters.Table_Capacity, TableCapacityRange, CHANCE_TO_REPLACE_PARAM, CHANCE_TO_MUTATE);

        // --- Evidential base length ---
        var EvidentialBaseLengthRange = GetEvidentialBaseLengthRange();
        MutateInt(ref this.personality_parameters.Evidential_Base_Length, EvidentialBaseLengthRange, CHANCE_TO_REPLACE_PARAM, CHANCE_TO_MUTATE);

        // --- Time Projection Event ---
        var timeProjectionEventRange = GetTimeProjectionEventRange();
        MutateFloat(ref this.personality_parameters.Time_Projection_Event, timeProjectionEventRange, CHANCE_TO_REPLACE_PARAM, CHANCE_TO_MUTATE);

        // --- Time Projection Goal ---
        var timeProjectionGoalRange = GetTimeProjectionGoalRange();
        MutateFloat(ref this.personality_parameters.Time_Projection_Goal, timeProjectionGoalRange, CHANCE_TO_REPLACE_PARAM, CHANCE_TO_MUTATE);

        // --- Compound Confidence ---
        var generalizationConfidenceRange = GetCompoundConfidenceRange();
        MutateFloat(ref this.personality_parameters.Compound_Confidence, generalizationConfidenceRange, CHANCE_TO_REPLACE_PARAM, CHANCE_TO_MUTATE);

        // flip compounding on/off
        var compoundRange = GetRuntimeCompoundRange();
        MutateBool(ref this.personality_parameters.RuntimeCompounds1, compoundRange, CHANCE_TO_TOGGLE_BOOL);
        MutateBool(ref this.personality_parameters.RuntimeCompounds2, compoundRange, CHANCE_TO_TOGGLE_BOOL);
        MutateBool(ref this.personality_parameters.RuntimeCompounds3, compoundRange, CHANCE_TO_TOGGLE_BOOL);
    }




    public void AddNewRandomBelief()
    {
        StatementTerm statement = CreateContingencyStatement(GetRandomSensoryTerm(), GetRandomMotorTerm(), GetRandomSensoryTerm());
        string statement_string = statement.ToString();
        if (!belief_statement_strings.ContainsKey(statement_string))
        {
            float f = UnityEngine.Random.Range(0.5f, 1f);
            float c = UnityEngine.Random.Range(0.0f, 1f);
            EvolvableSentence sentence = new(statement: statement,
                   new float2(f, c));
            this.beliefs.Add(sentence);
            belief_statement_strings.Add(statement_string, true);
        }
        else
        {
            Debug.LogWarning("genome already contained " + statement_string);
        }

    }

    public void RemoveRandomBelief()
    {
        if (this.beliefs.Count == 0) return;
        int rnd_idx = UnityEngine.Random.Range(0, this.beliefs.Count);
        var belief = this.beliefs[rnd_idx];
        this.beliefs.RemoveAt(rnd_idx);
        belief_statement_strings.Remove(belief.statement.ToString());
    }

    public void ModifyRandomBelief()
    {
        if (this.beliefs.Count == 0) return;
        int rnd_idx = UnityEngine.Random.Range(0, this.beliefs.Count);
        EvolvableSentence belief = this.beliefs[rnd_idx];

        string old_statement_string = belief.statement.ToString();

        // (S &/ ^M =/> P)
        StatementTerm implication = belief.statement;

        CompoundTerm subject = (CompoundTerm)implication.get_subject_term();
        Term S = (Term)subject.subterms[0];
        StatementTerm M = (StatementTerm)subject.subterms[1];
        Term P = (Term)implication.get_predicate_term();

        StatementTerm new_statement;
        int rnd = UnityEngine.Random.Range(0, 3);
        if (rnd == 0)
        {

            // replace S
            if (S is CompoundTerm sComp)
            {
                // replace 1 part of the compound
                List<Term> subterms = new();


                int rnd_subterm_idx = UnityEngine.Random.Range(0, sComp.subterms.Count);

                if (sComp.subterms.Count == 2)
                {
                    if (rnd_subterm_idx == 0)
                    {
                        subterms.Add(sComp.subterms[1]);
                    }
                    else if (rnd_subterm_idx == 1)
                    {
                        subterms.Add(sComp.subterms[0]);
                    }
                    else
                    {
                        Debug.LogError("null");
                        return;
                    }
                }
                else if (sComp.subterms.Count == 3)
                {
                    if (rnd_subterm_idx == 0)
                    {
                        subterms.Add(sComp.subterms[1]);
                        subterms.Add(sComp.subterms[2]);
                    }
                    else if (rnd_subterm_idx == 1)
                    {
                        subterms.Add(sComp.subterms[0]);
                        subterms.Add(sComp.subterms[2]);
                    }
                    else if (rnd_subterm_idx == 2)
                    {
                        subterms.Add(sComp.subterms[0]);
                        subterms.Add(sComp.subterms[1]);
                    }
                    else
                    {
                        Debug.LogError("null");
                        return;
                    }
                }
                List<StatementTerm> ignore_subterms = new();
                foreach (var t in subterms)
                {
                    ignore_subterms.Add((StatementTerm)t);
                }
                var randomS = GetRandomSensoryTerm(ignore_subterms);
                subterms.Add(randomS);
                var new_S = TermHelperFunctions.TryGetCompoundTerm(subterms, TermConnector.ParallelConjunction);
                new_statement = CreateContingencyStatement(new_S, M, P);
            }
            else if (S is StatementTerm)
            {
                var randomS = GetRandomSensoryTerm();
                new_statement = CreateContingencyStatement(randomS, M, P);
            }
            else
            {
                Debug.LogError("null");
                return;
            }
        }
        else if (rnd == 1)
        {
            // replace M
            var randomM = GetRandomMotorTerm();
            if (S.contains_variable())
            {
                randomM = new StatementTerm(Term.from_string("(*,{SELF},#x)"), randomM.get_predicate_term(), Copula.Inheritance);
            }
            new_statement = CreateContingencyStatement(subject.subterms[0], randomM, P);
        }
        else //if (rnd == 2)
        {
            // replace P
            if (P is CompoundTerm pComp)
            {
                // replace 1 part of the compound
                List<Term> subterms = new();

                int rnd_subterm_idx = UnityEngine.Random.Range(0, pComp.subterms.Count);

                if (pComp.subterms.Count == 2)
                {
                    if (rnd_subterm_idx == 0)
                    {
                        subterms.Add(pComp.subterms[1]);
                    }
                    else if (rnd_subterm_idx == 1)
                    {
                        subterms.Add(pComp.subterms[0]);
                    }
                    else
                    {
                        Debug.LogError("null");
                        return;
                    }
                }
                else if (pComp.subterms.Count == 3)
                {
                    if (rnd_subterm_idx == 0)
                    {
                        subterms.Add(pComp.subterms[1]);
                        subterms.Add(pComp.subterms[2]);
                    }
                    else if (rnd_subterm_idx == 1)
                    {
                        subterms.Add(pComp.subterms[0]);
                        subterms.Add(pComp.subterms[2]);
                    }
                    else if (rnd_subterm_idx == 2)
                    {
                        subterms.Add(pComp.subterms[0]);
                        subterms.Add(pComp.subterms[1]);
                    }
                    else
                    {
                        Debug.LogError("null");
                        return;
                    }
                }
                List<StatementTerm> ignore_subterms = new();
                foreach (var t in subterms)
                {
                    ignore_subterms.Add((StatementTerm)t);
                }
                var randomP = GetRandomSensoryTerm(ignore_subterms);
                subterms.Add(randomP);
                var new_P = TermHelperFunctions.TryGetCompoundTerm(subterms, TermConnector.ParallelConjunction);
                new_statement = CreateContingencyStatement(S, M, new_P);
            }
            else if (P is StatementTerm)
            {
                var randomP = GetRandomSensoryTerm();
                new_statement = CreateContingencyStatement(S, M, randomP);
            }
            else
            {
                Debug.LogError("null");
                return;
            }
        }



        belief.statement = new_statement;
        string new_statement_string = new_statement.ToString();
        if (belief_statement_strings.ContainsKey(new_statement_string)) return;

        belief_statement_strings.Remove(old_statement_string);
        belief_statement_strings.Add(new_statement_string, true);

        this.beliefs[rnd_idx] = belief;
    }

    public StatementTerm GetRandomSensoryTerm()
    {
        //if (UnityEngine.Random.value < 0.05) return energy_increasing;
        int rnd = UnityEngine.Random.Range(0, SENSORY_TERM_SET.Count);
        return SENSORY_TERM_SET[rnd];
    }

    public StatementTerm GetRandomMotorTerm()
    {
        int rnd = UnityEngine.Random.Range(0, MOTOR_TERM_SET.Count);
        return MOTOR_TERM_SET[rnd];
    }

    private int GetRandomIndexSkipping(int count, IReadOnlyCollection<int> skipIdxs)
    {
        if (count <= 0)
            throw new InvalidOperationException("Cannot pick from an empty collection.");

        // No skips? Just pick normally.
        if (skipIdxs == null || skipIdxs.Count == 0)
            return UnityEngine.Random.Range(0, count);

        // Clamp skip indices to valid range and put in a HashSet for O(1) lookup
        var banned = new HashSet<int>();
        foreach (var idx in skipIdxs)
        {
            if (idx >= 0 && idx < count)
                banned.Add(idx);
        }

        // If we banned everything, there's nothing to pick
        if (banned.Count >= count)
            throw new InvalidOperationException("All indices are skipped. Cannot pick any.");

        // We want a random index among the *allowed* slots.
        int allowedCount = count - banned.Count;
        int rndPos = UnityEngine.Random.Range(0, allowedCount); // position within allowed indices

        // Walk through indices and pick the rndPos-th allowed one
        for (int i = 0; i < count; i++)
        {
            if (banned.Contains(i))
                continue;

            if (rndPos == 0)
                return i;

            rndPos--;
        }

        // Should be unreachable
        throw new InvalidOperationException("Random selection failed unexpectedly.");
    }


    private int GetRandomIndexSkipping(int count, int skipIdx)
    {
        if (count <= 0)
            throw new InvalidOperationException("Cannot pick from an empty collection.");

        // If skip is out of range or there's only one element, just pick normally
        if (skipIdx < 0 || skipIdx >= count || count == 1)
            return UnityEngine.Random.Range(0, count);

        // Pick from 0..count-2 (since upper bound is exclusive)
        int rnd = UnityEngine.Random.Range(0, count - 1);

        // Shift up if we hit or pass the skipped index
        if (rnd >= skipIdx)
            rnd++;

        return rnd;
    }
    public StatementTerm GetRandomSensoryTerm(StatementTerm ignoreTerm)
    {
        int ignoreIdx = SENSORY_TERM_SET.IndexOf(ignoreTerm);
        int rnd = GetRandomIndexSkipping(SENSORY_TERM_SET.Count, ignoreIdx);
        return SENSORY_TERM_SET[rnd];
    }

    public StatementTerm GetRandomMotorTerm(StatementTerm ignoreTerm)
    {
        int ignoreIdx = MOTOR_TERM_SET.IndexOf(ignoreTerm);
        int rnd = GetRandomIndexSkipping(MOTOR_TERM_SET.Count, ignoreIdx);
        return MOTOR_TERM_SET[rnd];
    }

    public StatementTerm GetRandomSensoryTerm(IReadOnlyCollection<StatementTerm> ignoreTerms)
    {
        var ignoreIdxs = new List<int>();

        if (ignoreTerms != null)
        {
            foreach (var term in ignoreTerms)
            {
                int idx = SENSORY_TERM_SET.IndexOf(term);
                if (idx >= 0)
                    ignoreIdxs.Add(idx);
            }
        }

        int rnd = GetRandomIndexSkipping(SENSORY_TERM_SET.Count, ignoreIdxs);
        return SENSORY_TERM_SET[rnd];
    }

    public StatementTerm GetRandomMotorTerm(IReadOnlyCollection<StatementTerm> ignoreTerms)
    {
        var ignoreIdxs = new List<int>();

        if (ignoreTerms != null)
        {
            foreach (var term in ignoreTerms)
            {
                int idx = MOTOR_TERM_SET.IndexOf(term); // <-- fixed to MOTOR_TERM_SET
                if (idx >= 0)
                    ignoreIdxs.Add(idx);
            }
        }

        int rnd = GetRandomIndexSkipping(MOTOR_TERM_SET.Count, ignoreIdxs);
        return MOTOR_TERM_SET[rnd];
    }


    public void AddNewBelief(EvolvableSentence belief)
    {
        string statement_string = belief.statement.ToString();
        if (!belief_statement_strings.ContainsKey(statement_string))
        {
            float f = UnityEngine.Random.Range(0.5f, 1f);
            this.beliefs.Add(belief);
            belief_statement_strings.Add(statement_string, true);
        }
        else
        {
            Debug.LogWarning("genome already contained " + statement_string);
        }

    }

    public (NARSGenome, NARSGenome) Reproduce(NARSGenome parent2genome)
    {
        NARSGenome parent1 = this;
        NARSGenome parent2 = (NARSGenome)parent2genome;
        int longer_array = math.max(parent1.beliefs.Count, parent2.beliefs.Count);

        NARSGenome offspring1 = new();
        offspring1.beliefs.Clear();
        offspring1.belief_statement_strings.Clear();
        NARSGenome offspring2 = new();
        offspring2.beliefs.Clear();
        offspring2.belief_statement_strings.Clear();

        if (USE_AND_EVOLVE_CONTINGENCIES())
        {
            for (int i = 0; i < longer_array; i++)
            {
                int rnd = UnityEngine.Random.Range(0, 2);

                if (rnd == 0)
                {
                    if (i < parent1.beliefs.Count) offspring1.AddNewBelief(parent1.beliefs[i]);
                    if (i < parent2.beliefs.Count) offspring2.AddNewBelief(parent2.beliefs[i]);
                }
                else
                {
                    if (i < parent2.beliefs.Count) offspring1.AddNewBelief(parent2.beliefs[i]);
                    if (i < parent1.beliefs.Count) offspring2.AddNewBelief(parent1.beliefs[i]);
                }
            }
        }

        if (EVOLVE_PERSONALITY())
        {
            for (int i = 0; i < PersonalityParameters.GetParameterCount(); i++)
            {
                int rnd = UnityEngine.Random.Range(0, 2);
                if (rnd == 0)
                {
                    offspring1.personality_parameters.Set(i, parent1.personality_parameters.Get(i));
                    offspring2.personality_parameters.Set(i, parent2.personality_parameters.Get(i));
                }
                else
                {
                    offspring1.personality_parameters.Set(i, parent2.personality_parameters.Get(i));
                    offspring2.personality_parameters.Set(i, parent1.personality_parameters.Get(i));
                }
            }
        }



        return (offspring1, offspring2);
    }

    public float CalculateHammingDistance(NARSGenome other_genome)
    {
        int distance = 0;
        NARSGenome genome1 = this;
        NARSGenome genome2 = (NARSGenome)other_genome;

        for (int i = 0; i < genome1.beliefs.Count; i++)
        {
            var belief1 = genome1.beliefs[i];
            if (!genome2.belief_statement_strings.ContainsKey(belief1.statement.ToString()))
            {
                distance++;
            }
        }


        for (int j = 0; j < genome2.beliefs.Count; j++)
        {
            var belief2 = genome2.beliefs[j];
            if (!genome1.belief_statement_strings.ContainsKey(belief2.statement.ToString()))
            {
                distance++;
            }
        }

        return distance;
    }

    internal void SetIdealGoal(BlocksWorld blocksworld)
    {
        goals.Clear();
        List<(StatementTerm, float?, float?)> statement_strings = new();
        foreach (var goal_statement in blocksworld.GetGoalStateNoClear(out var _))
        {
            statement_strings.Add((goal_statement, null, null));
        }
        AddEvolvableSentences(goals, statement_strings.ToArray());
    }
}

public static class MutationHelpers
{

    static System.Random r = new();
    public static double GetPerturbationFromRange(double min, double max, double fraction = 0.1f)
    {
        double range = max - min;
        double stdDev = range * fraction;

        // Generate standard normal sample using Box-Muller
        double u, v, S;
        do
        {
            u = 2.0 * r.NextDouble() - 1.0;
            v = 2.0 * r.NextDouble() - 1.0;
            S = u * u + v * v;
        } while (S >= 1.0 || S == 0);

        double fac = Math.Sqrt(-2.0 * Math.Log(S) / S);
        double result = u * fac;

        result *= stdDev; // scale

        return result;
    }

    public static double GetPerturbationFromRange(Vector2 range, double fraction = 0.1f)
    {
        return GetPerturbationFromRange(range.x, range.y, fraction);
    }


    public static double GetPerturbationFromRange(Vector2Int range, double fraction = 0.1f)
    {
        return GetPerturbationFromRange(range.x, range.y, fraction);
    }
}