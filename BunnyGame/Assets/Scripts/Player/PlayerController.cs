﻿using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class PlayerController : NetworkBehaviour {


	public float walkSpeed = 5;
	public float runSpeed = 12;
	public float gravity = -12;
	public float jumpHeight = 3;

	[Range(0, 1)]
	public float airControlPercent;

	public float turnSmoothTime = 0.2f;
	public float speedSmoothTime = 0.2f;

	public float currentSpeed;
	public float velocityY;


	private bool _CC = false; //Turns off players ability to control character, used for CC effects

	private float _turnSmoothVelocity;
	private float _speedSmoothVelocity;


	private Transform _cameraTransform;
	public CharacterController controller;
	private PlayerEffects playerEffects;
	private PlayerAbilityManager _abilityManager;
	private PlayerHealth _playerHealth;

	public bool running = false;

	public bool inWater = false;

	private bool _moveDirectionLocked = false;
	private float _targetRotation = 0;
	private bool _noInputMovement = false;

	void Start() {
		CorrectRenderingMode(); // Calling this here to fix the rendering order of the model, because materials have rendering mode fade

		if (!this.isLocalPlayer)
			return;

		this._cameraTransform = Camera.main.transform;
		this.controller       = this.GetComponent<CharacterController>();
		this.playerEffects    = this.GetComponent<PlayerEffects>();
		this._abilityManager  = this.GetComponent<PlayerAbilityManager>();
		this._playerHealth    = this.GetComponent<PlayerHealth>();

		this.airControlPercent = 1;
        spawn();
	}


    void Update() {
        if (!this.isLocalPlayer) // NB! wallDamage should now work on clients
            return;

        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector2 inputDir = input.normalized;
        running = Input.GetKey(KeyCode.LeftShift);
        _moveDirectionLocked = Input.GetKey(KeyCode.LeftAlt);

        if (!this._CC) {
            if (!this._noInputMovement)
                    Move(inputDir);
            else
                NoInputMovement();

            if (Input.GetKeyDown(KeyCode.Space) && !this.inWater)
                this.jump();
        }

        HandleAiming();
    }

    public bool getGrounded() {
        return controller.isGrounded;
    }

    public void setCC(bool value) {
        this._CC = value;
        if (value) {
            this.currentSpeed = 0;
            this.velocityY = 0;
        }
    }

    public bool getCC() {
        return this._CC;
    }

    public void spawn() {
        StartCoroutine(spawnEffect());
    }

    private IEnumerator spawnEffect() {
        while (!WorldData.ready) yield return 0;
        GetComponent<PlayerEffects>().CmdAddTrail(2.0f);
        const float speed = 0.5f;
        float t = 0;

        Vector3[] spline = new Vector3[3];
        spline[2] = WorldData.worldGrid.getRandomCell(false, 1).pos;
        spline[0] = spline[2];
        spline[0] += spline[0].normalized * 100;
        
        spline[1] = Vector3.Lerp(spline[0], spline[2], 0.5f);
        spline[1] += Vector3.up *  100;

        this._playerHealth.maxHeal();
        while (t < 1) {
            GetComponent<PlayerController>().velocityY = 0;
            transform.position = getSplinePos(spline, t);
            t += Time.deltaTime * speed;            
            yield return 0;
        }
        this._playerHealth.maxHeal();
    }

    private Vector3 getSplinePos(Vector3[] spline, float t) {
        Vector3 t1 = Vector3.Lerp(spline[0], spline[1], t);
        Vector3 t2 = Vector3.Lerp(spline[1], spline[2], t);
        return Vector3.Lerp(t1, t2, t);
    }

    // Turn off and on MeshRenderer so FPS camera works
    private void HandleAiming(){
        if (Input.GetKeyDown(KeyCode.Mouse1)) {
            foreach (Transform t in this.gameObject.transform.GetChild(1)) {
                if(t.gameObject.GetComponent<MeshRenderer>() != null)
                    t.gameObject.GetComponent<MeshRenderer>().enabled = false;
                else if(t.gameObject.GetComponent<SkinnedMeshRenderer>() != null)
                    t.gameObject.GetComponent<SkinnedMeshRenderer>().enabled = false;
            }
        }
        else if(Input.GetKeyUp(KeyCode.Mouse1)) {
            foreach (Transform t in this.gameObject.transform.GetChild(1)) {
                if (t.gameObject.GetComponent<MeshRenderer>() != null)
                    t.gameObject.GetComponent<MeshRenderer>().enabled = true;
                else if (t.gameObject.GetComponent<SkinnedMeshRenderer>() != null)
                    t.gameObject.GetComponent<SkinnedMeshRenderer>().enabled = true;
            }
        }
    }

    public void Move(Vector2 inputDir) {
        if(!_moveDirectionLocked)
            _targetRotation = _cameraTransform.eulerAngles.y;

        transform.eulerAngles = Vector3.up * Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation,
                                                                   ref _turnSmoothVelocity, GetModifiedSmoothTime(turnSmoothTime));

        float targetSpeed = ((running) ? runSpeed : walkSpeed) * inputDir.magnitude;
        if (inWater)
            targetSpeed *= 0.5f;

        Vector3 moveDir = transform.TransformDirection(new Vector3(inputDir.x, 0, inputDir.y));
        moveDir.y = 0;

        float slopeEffect = 1f;
        RaycastHit hit;
        if (Physics.SphereCast(transform.position, .25f, Vector3.down, out hit, 3f) && controller.isGrounded) {
            float slope = Vector3.Dot(new Vector3(moveDir.z, moveDir.y, -moveDir.x), (Vector3.Cross(Vector3.up, hit.normal)));
            slopeEffect = Mathf.Clamp(slope, -1, 0);
            slopeEffect = 1- Mathf.Pow(slopeEffect, 6);
        }

        this.currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref _speedSmoothVelocity, GetModifiedSmoothTime(speedSmoothTime))  * slopeEffect;

        this.velocityY += Time.deltaTime * gravity * (this.inWater ? 0 : 1);



        // Water y-dir movement
        if(this.inWater) {
            if (Input.GetKey(KeyCode.Space))
                velocityY += 2f;
            else if (Input.GetKey(KeyCode.C))
                velocityY -= 2f;

            velocityY -= Mathf.Sign(velocityY) * 0.2f;
            velocityY = Mathf.Clamp(velocityY, -10, 10);
        }

        Vector3 velocity = moveDir.normalized * currentSpeed * playerEffects.getSpeed() + Vector3.up * velocityY;
        this.controller.Move(velocity * Time.deltaTime);


        if (controller.isGrounded)
            velocityY = 0;
    }

    public bool jump() {
        if ((controller.isGrounded) && !onWall(0.1f)) { 
            float jumpVelocity = Mathf.Sqrt(-2 * gravity * jumpHeight * this.playerEffects.getJump()); 
            this.velocityY = jumpVelocity;
            return true;
        }
        return false;
    }

    //Controll player in air after jump
    float GetModifiedSmoothTime(float smoothTime) {
        if (controller.isGrounded || this.inWater)
            return smoothTime;

        if (smoothTime == 0)
            return float.MaxValue;

        return smoothTime / airControlPercent;
    }

    private void OnDestroy() {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

	public void CorrectRenderingMode() {
		Material[] materials;

		foreach (Transform child in this.transform.GetChild(1)) {
			if (child.gameObject.GetComponent<Renderer>() != null)
				materials = child.gameObject.GetComponent<Renderer>().materials;
			else if (child.gameObject.GetComponent<SkinnedMeshRenderer>() != null)
				materials = child.gameObject.GetComponent<SkinnedMeshRenderer>().materials;
			else
				continue;

			foreach (Material mat in materials) {
				mat.SetInt("_ZWrite", 1);
				mat.renderQueue = 2000;
			}
		}
	}

	private bool onWall(float offset) {
		const float deltaLimit = 0.2f;
		Vector3[] offsets = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };

		float[] distances = new float[offsets.Length];
		RaycastHit hit = new RaycastHit();
		int layerMask = (1 << 19);
		for (int i = 0; i < offsets.Length; i++) {
			Ray ray = new Ray(transform.position + offsets[i] * offset + Vector3.up, Vector3.down);
			Physics.Raycast(ray, out hit, 10.0f, layerMask);
			distances[i] = hit.distance;
		}

		foreach (var dist in distances) {
			foreach (var dist2 in distances) {
				if (Mathf.Abs(dist - dist2) > deltaLimit) {
					return true;
				}

			}
		}
		return false;
	}

	// Used in SpeedBomb ability
	public void NoInputMovement()
	{
		if (!_moveDirectionLocked)
			_targetRotation = _cameraTransform.eulerAngles.y;

		transform.eulerAngles = Vector3.up * Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation,
			ref _turnSmoothVelocity, GetModifiedSmoothTime(turnSmoothTime));

		float targetSpeed = ((running) ? runSpeed : walkSpeed);
		this.currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref _speedSmoothVelocity, GetModifiedSmoothTime(speedSmoothTime));

		this.velocityY += Time.deltaTime * gravity;

		Vector3 moveDir = transform.TransformDirection(new Vector3(transform.eulerAngles.x, 0, transform.eulerAngles.y));
		moveDir.y = 0;

		Vector3 velocity = moveDir.normalized * currentSpeed * playerEffects.getSpeed() + Vector3.up * velocityY;

		this.controller.Move(velocity * Time.deltaTime);

		if (controller.isGrounded)
			velocityY = 0;
	}

	public void setNoInputMovement(bool noInput)
	{
		this._noInputMovement = noInput;
	}


	void OnControllerColliderHit(ControllerColliderHit hit) {
		if (transform.position.y < hit.point.y) {
			this.velocityY = (this.velocityY > 0) ? 0 : this.velocityY;
		}
	}
	
}