using System;
using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Loads the Main scene in Play mode and runs TosakanaRequirementsRunner (in the game's own assembly),
// then fails if any of its checks failed. Run from the command line with -runTests -testPlatform PlayMode.
public class TosakanaPlayTest
{
	[UnityTest, Timeout(1000000)]
	public IEnumerator AllRequirementsPass()
	{
		var log = new StringBuilder();
		string done = null;
		Application.LogCallback listen = (message, stack, type) =>
		{
			if (message.StartsWith("TOSAKANA-CHECK")) log.AppendLine(message);
			if (message.StartsWith("TOSAKANA-CHECK DONE")) done = message;
		};
		Application.logMessageReceived += listen;

		SceneManager.LoadScene("Main");
		yield return null;
		yield return null;

		Type runner = Type.GetType("TosakanaRequirementsRunner, Assembly-CSharp");
		new GameObject("TosakanaRequirementsRunner").AddComponent(runner);

		float timeout = Time.realtimeSinceStartup + 900;
		while (done == null && Time.realtimeSinceStartup < timeout)
			yield return null;

		Application.logMessageReceived -= listen;
		Debug.Log("TOSAKANA-RESULTS\n" + log);
		Assert.IsNotNull(done, "Runner did not finish");
		StringAssert.Contains(" 0 failed", done);
	}
}
