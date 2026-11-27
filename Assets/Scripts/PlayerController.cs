using UnityEngine;

// Walk, run and jump. Moves the way the camera is looking.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
	public float walkSpeed = 4.5f;
	public float runSpeed = 9f;
	public float jumpSpeed = 6f;
	public float gravity = 20f;
	public float turnSpeed = 720f;

	[Header("Body parts that swing when walking")]
	public Transform leftLeg;
	public Transform rightLeg;
	public Transform leftArm;
	public Transform rightArm;

	// Test hooks for the play-mode checks
	[HideInInspector] public Vector2 testMove;
	[HideInInspector] public bool testRun;

	public float CurrentSpeed { get; private set; }

	CharacterController cc;
	float verticalSpeed;
	float walkCycle;

	void Awake()
	{
		cc = GetComponent<CharacterController>();
	}

	void Update()
	{
		TosakanaGame game = TosakanaGame.Instance;
		bool playing = game == null || game.IsPlaying;
		float dt = Time.deltaTime;

		Vector2 input = testMove;
		bool run = testRun;
		if (playing)
		{
			input += new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
			run |= Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
		}
		else
			input = Vector2.zero;
		input = Vector2.ClampMagnitude(input, 1f);

		// Move the way the camera looks
		float yaw = game != null ? game.cameraRig.Yaw : transform.eulerAngles.y;
		Vector3 move = Quaternion.Euler(0, yaw, 0) * new Vector3(input.x, 0, input.y);
		float speed = run ? runSpeed : walkSpeed;
		if (move.sqrMagnitude > 0.01f)
			transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(move), turnSpeed * dt);

		if (cc.isGrounded)
		{
			verticalSpeed = -1f;
			if (playing && Input.GetKeyDown(KeyCode.Space))
				verticalSpeed = jumpSpeed;
		}
		else
			verticalSpeed -= gravity * dt;

		cc.Move((move * speed + Vector3.up * verticalSpeed) * dt);
		CurrentSpeed = move.magnitude * speed;

		// Swing arms and legs
		walkCycle += dt * CurrentSpeed * 1.7f;
		float swing = Mathf.Sin(walkCycle) * Mathf.Clamp01(CurrentSpeed / 3f) * 35f;
		if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(swing, 0, 0);
		if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(-swing, 0, 0);
		if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-swing * 0.8f, 0, 0);
		if (rightArm != null) rightArm.localRotation = Quaternion.Euler(swing * 0.8f, 0, 0);
	}

	public void Teleport(Vector3 position, float yaw)
	{
		if (cc == null)
			cc = GetComponent<CharacterController>();
		cc.enabled = false;
		transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
		cc.enabled = true;
		verticalSpeed = 0;
	}
}
