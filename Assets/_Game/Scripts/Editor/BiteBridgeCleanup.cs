using UnityEditor;
using UnityEditor.Animations;
using System.Linq;

// Utility to collapse the Animator StateMachineBehaviour bridges into the C# FSM.
// Run from the Unity menu (Cleanup > Remove Bite Bridges). It strips
// ZombieBiteBehaviour / TakeBiteBehavior from the animator controllers and deletes
// the now-unused scripts. Safe to delete after running.
public static class BiteBridgeCleanup
{
    private static readonly string[] BridgeBehaviourNames =
    {
        "ZombieBiteBehaviour",
        "TakeBiteBehavior",
    };

    [MenuItem("Cleanup/Remove Bite Bridges")]
    public static void RemoveBiteBehaviours()
    {
        RemoveFrom("Assets/_Game/Animations/Characters/Zombie/AC_Zombie.controller");
        RemoveFrom("Assets/_Game/Animations/Characters/Survivor/AC_FemaleSurvivor.controller");

        AssetDatabase.DeleteAsset("Assets/_Game/Scripts/Characters/AI/Entities/ZombieAI/AnimationStates/ZombieBiteBehaviour.cs");
        AssetDatabase.DeleteAsset("Assets/_Game/Scripts/Characters/Player/StateMachine/AnimationBehaviours/TakeBiteBehavior.cs");
        AssetDatabase.SaveAssets();
    }

    private static void RemoveFrom(string path)
    {
        var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (ac == null)
        {
            UnityEngine.Debug.LogWarning("[BiteBridgeCleanup] Controller not found: " + path);
            return;
        }

        Recurse(ac.layers[0].stateMachine);
        EditorUtility.SetDirty(ac);
    }

    private static void Recurse(AnimatorStateMachine stateMachine)
    {
        foreach (var childState in stateMachine.states)
        {
            var behaviours = childState.state.behaviours;
            var filtered = behaviours
                .Where(b => b == null || System.Array.IndexOf(BridgeBehaviourNames, b.GetType().Name) < 0)
                .ToArray();

            if (filtered.Length != behaviours.Length)
            {
                childState.state.behaviours = filtered;
            }
        }

        foreach (var child in stateMachine.stateMachines)
        {
            Recurse(child.stateMachine);
        }
    }
}
