using System;
using UnityEngine;
using static BlocksWorldGridManager;

public class NARSBody
{
    internal int remaining_life = 10;
    internal int timesteps_alive;
    NARS nars;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public NARSBody(NARS nars)
    {
        this.nars = nars;
    }

    public void Sense(BlocksWorld blocksWorld)
    {
        var current_states = blocksWorld.GetCurrentState();
        foreach(var state_term in current_states)
        {
            var sensation = new Judgment(this.nars, state_term, new(1.0f, 0.99f));
            nars.SendInput(sensation);
        }

    }

    public void MotorAct(BlocksWorld blocksWorld)
    {
        float max_motor_activation = 0.0f;
        StatementTerm highest_desire_motor_op =null;
        foreach (var motor_statement in NARSGenome.MOTOR_TERM_SET)
        {
            var moveTerm = motor_statement;
            float activation = this.nars.GetGoalActivation(moveTerm);
            if (activation < this.nars.config.T) continue;
            if (activation > max_motor_activation)
            {
                highest_desire_motor_op = motor_statement;
                max_motor_activation = activation;
            }
        }

        if (highest_desire_motor_op == null) return;
        CompoundTerm subject_term = (CompoundTerm)highest_desire_motor_op.get_subject_term();
        var term1 = subject_term.subterms[1];
       
        if (highest_desire_motor_op.get_predicate_term().term_string == "STACK")
        {
            var term2 = subject_term.subterms[2];
            blocksWorld.Stack(term1.term_string, term2.term_string);
        }else if (highest_desire_motor_op.get_predicate_term().term_string == "UNSTACK")
        {
            blocksWorld.Unstack(term1.term_string);
        }

    }
}
