using UnityEngine;

// A person's body with real joints: hips and knees, shoulders and elbows, a spine and a head.
// The scripts that move people (you, people in the street, police) call these to pose the body:
// walking with bent knees, a fighting guard, jab, cross, hook, kick, block, getting hit.
public class Body : MonoBehaviour
{
	public Transform spine, head;
	public Transform leftShoulder, rightShoulder, leftElbow, rightElbow;
	public Transform leftHip, rightHip, leftKnee, rightKnee;

	public enum Move { None, Jab, Cross, Hook, Kick, Block, Hit, Cheer, Windup }

	// Walking / running. cycle = where in the step we are, amount = 0 standing .. 1 running
	public void Walk(float cycle, float amount)
	{
		float s = Mathf.Sin(cycle);
		float leg = s * 34f * amount;
		Rot(leftHip, leg, 0, 0);
		Rot(rightHip, -leg, 0, 0);
		// The knee bends when the leg swings back and lifts
		Rot(leftKnee, Mathf.Max(0f, Mathf.Sin(cycle + 1.2f)) * 55f * amount + 4f, 0, 0);
		Rot(rightKnee, Mathf.Max(0f, Mathf.Sin(cycle + 1.2f + Mathf.PI)) * 55f * amount + 4f, 0, 0);
		Rot(leftShoulder, -leg * 0.8f, 0, 4f);
		Rot(rightShoulder, leg * 0.8f, 0, -4f);
		Rot(leftElbow, -12f - 30f * amount, 0, 0);
		Rot(rightElbow, -12f - 30f * amount, 0, 0);
		Rot(spine, 8f * amount, 0, 0);
		Rot(head, -4f * amount, 0, 0);
	}

	// Fists up, ready to fight. Legs a little apart.
	public void Guard(float bob = 0f)
	{
		Rot(leftShoulder, -55f, 10f, 18f);
		Rot(rightShoulder, -45f, -10f, -18f);
		Rot(leftElbow, -115f, 0, 0);
		Rot(rightElbow, -120f, 0, 0);
		Rot(leftHip, -12f, 0, 6f);
		Rot(rightHip, 10f, 0, -6f);
		Rot(leftKnee, 18f + bob, 0, 0);
		Rot(rightKnee, 14f + bob, 0, 0);
		Rot(spine, 8f, -10f, 0);
		Rot(head, -6f, 8f, 0);
	}

	// A fighting move. t goes from 0 (start) to 1 (finished).
	public void Do(Move move, float t)
	{
		float hit = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);   // out and back
		switch (move)
		{
			case Move.Jab:   // quick left straight
				Guard();
				Rot(leftShoulder, Mathf.Lerp(-55f, -92f, hit), Mathf.Lerp(10f, -4f, hit), 8f);
				Rot(leftElbow, Mathf.Lerp(-115f, -5f, hit), 0, 0);
				Rot(spine, 8f, Mathf.Lerp(-10f, -22f, hit), 0);
				break;
			case Move.Cross: // strong right straight, the body turns
				Guard();
				Rot(rightShoulder, Mathf.Lerp(-45f, -92f, hit), Mathf.Lerp(-10f, 6f, hit), -8f);
				Rot(rightElbow, Mathf.Lerp(-120f, -5f, hit), 0, 0);
				Rot(spine, 10f, Mathf.Lerp(-10f, 28f, hit), 0);
				break;
			case Move.Hook:  // big swinging right hook
				Guard();
				Rot(rightShoulder, -88f, Mathf.Lerp(-60f, 35f, Mathf.Clamp01(t * 1.4f)), Mathf.Lerp(-18f, -70f, hit));
				Rot(rightElbow, -80f, 0, 0);
				Rot(spine, 6f, Mathf.Lerp(-25f, 40f, Mathf.Clamp01(t * 1.4f)), 0);
				break;
			case Move.Kick:  // front kick with the right leg
				Guard();
				Rot(rightHip, Mathf.Lerp(10f, -95f, hit), 0, 0);
				Rot(rightKnee, Mathf.Lerp(90f, 5f, Mathf.Clamp01(t * 2f - 0.3f)) * (t < 0.85f ? 1f : 4f * (1f - t) + 0.2f), 0, 0);
				Rot(leftKnee, 12f, 0, 0);
				Rot(spine, Mathf.Lerp(8f, -18f, hit), 0, 0);
				break;
			case Move.Block: // both arms cover the face
				Rot(leftShoulder, -105f, 25f, 10f);
				Rot(rightShoulder, -105f, -25f, -10f);
				Rot(leftElbow, -135f, 0, 0);
				Rot(rightElbow, -135f, 0, 0);
				Rot(leftKnee, 20f, 0, 0);
				Rot(rightKnee, 20f, 0, 0);
				Rot(spine, 18f, 0, 0);
				Rot(head, 12f, 0, 0);
				break;
			case Move.Hit:   // got hit: the head and body snap back
				Guard();
				Rot(spine, Mathf.Lerp(8f, -24f, hit), Mathf.Lerp(0, 15f, hit), 0);
				Rot(head, Mathf.Lerp(-6f, -30f, hit), 0, 0);
				Rot(leftShoulder, -30f, 0, Mathf.Lerp(18f, 45f, hit));
				Rot(rightShoulder, -30f, 0, Mathf.Lerp(-18f, -45f, hit));
				break;
			case Move.Windup: // pulls the right fist back before punching (watch for it, then block or dodge!)
				Guard();
				Rot(rightShoulder, Mathf.Lerp(-45f, 25f, t), -20f, -25f);
				Rot(rightElbow, -110f, 0, 0);
				Rot(spine, 6f, Mathf.Lerp(-10f, -30f, t), 0);
				break;
			case Move.Cheer:  // watching a fight: arms up
				float wave = Mathf.Sin(Time.time * 7f + transform.position.x) * 20f;
				Rot(leftShoulder, -160f + wave, 0, 15f);
				Rot(rightShoulder, -160f - wave, 0, -15f);
				Rot(leftElbow, -20f, 0, 0);
				Rot(rightElbow, -20f, 0, 0);
				Rot(leftHip, 0, 0, 0); Rot(rightHip, 0, 0, 0);
				Rot(leftKnee, 3f, 0, 0); Rot(rightKnee, 3f, 0, 0);
				Rot(spine, -3f, 0, 0);
				Rot(head, -10f, 0, 0);
				break;
		}
	}

	// Lying on the ground: arms out a little, one knee bent
	public void Lie()
	{
		Rot(leftShoulder, -20f, 0, 40f);
		Rot(rightShoulder, -10f, 0, -55f);
		Rot(leftElbow, -30f, 0, 0);
		Rot(rightElbow, -10f, 0, 0);
		Rot(leftHip, -25f, 0, 8f);
		Rot(rightHip, 0, 0, -6f);
		Rot(leftKnee, 50f, 0, 0);
		Rot(rightKnee, 5f, 0, 0);
		Rot(spine, 0, 0, 0);
		Rot(head, -10f, 25f, 0);
	}

	// Standing still
	public void Stand()
	{
		Walk(0f, 0f);
	}

	static void Rot(Transform t, float x, float y, float z)
	{
		if (t != null)
			t.localRotation = Quaternion.Euler(x, y, z);
	}
}
