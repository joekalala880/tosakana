using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Run with: -runTests -testPlatform PlayMode -testFilter TosakanaSnapshot
[Explicit("Saves preview pictures; run on purpose with -testFilter TosakanaSnapshot")]
public class TosakanaSnapshot
{
	[UnityTest, Timeout(700000)]
	public IEnumerator TakePictures()
	{
		SceneManager.LoadScene("Main");
		yield return null;
		Type taker = Type.GetType("TosakanaSnapshotTaker, Assembly-CSharp");
		Component c = new GameObject("Snapshot").AddComponent(taker);
		var finished = taker.GetProperty("Finished");
		float timeout = Time.realtimeSinceStartup + 600;
		while (!(bool)finished.GetValue(c) && Time.realtimeSinceStartup < timeout)
			yield return null;
		Assert.IsTrue((bool)finished.GetValue(c));
	}
}
