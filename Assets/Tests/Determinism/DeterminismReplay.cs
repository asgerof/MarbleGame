using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

public class DeterminismReplay
{
    [Test]
    public void WorldsStayInLockstep()
    {
        var worldA = new World("A");
        var worldB = new World("B");

        // build both worlds the same way (your bootstrap or helper here)
        WorldBootstrap.Boot(worldA);
        WorldBootstrap.Boot(worldB);

        const int ticks = 1_000;
        for (int i = 0; i < ticks; i++)
        {
            worldA.Update();
            worldB.Update();
        }

        var hashA = SimulationHasher.GetHash(worldA);
        var hashB = SimulationHasher.GetHash(worldB);

        Assert.AreEqual(hashA, hashB);
        worldA.Dispose(); worldB.Dispose();
    }
} 