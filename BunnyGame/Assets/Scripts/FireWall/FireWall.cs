﻿using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

public class FireWall : NetworkBehaviour {
    public class Circle {
        public Vector3 _pos;
        private float _radius;
        public GameObject wall;

        public Circle(float radius, Vector3 pos) {
            this._radius = radius;
            this._pos = pos;

            this.wall = Resources.Load<GameObject>("Prefabs/WallShell");
            this.wall = MonoBehaviour.Instantiate(this.wall);
            this.wall.transform.position = pos;
            this.wall.transform.localScale = new Vector3(this.radius * 2, 300, this.radius * 2);
        }

        public Vector3 pos {
            get {
                return _pos;
            }
            set {
                _pos = value;
                wall.transform.position = value;
            }
        }

        public float radius {
            get {
                return _radius;
            }
            set {
                _radius = value;
                wall.transform.localScale = new Vector3(value * 2, 300, value * 2);
            }
        }
    }
 
    private const float _wallShrinkTime = 60.0f;//Time in seconds between _wall shrinking
    private const float _wallShrinkRate = 0.04f; //The rate at which the wall shrinks

    private WallMapRenderer _actualWallRenderer;//Renders the actual fire wall
    private WallMapRenderer _targetWallRenderer;//Renders the target fire wall
    private RectTransform   _wallTransitionUI;  //The little onscreen bar indicating when the wall will shrink
    private Image           _outsideWallEffect; //A red transparent UI panel indicating that the player is outside the wall
    private Circle          _current;           //The current circle
    private Circle          _target;            //The target circle
    private GameObject      _fire;              //Particle effect for fire
    private System.Random   _RNG;               //Number generator, will be seeded the same across all clients
    [SyncVar(hook="init")]
    private int             _rngSeed;
    private float           _wallShrinkTimer;   //Timer for when to shrink _wall   
    private bool            _wallIsShrinking;   //Keeps track of wheter or not the wall is shrinking
    private float           _outerBounds;       //Outer bounds of map
    private bool            _ready = false;     //Wall ready

    void Start() {
        _outerBounds = 250;
        this._fire = Resources.Load<GameObject>("Prefabs/Fire");
        if (this.isServer) StartCoroutine(waitForClients());
    }

    //Waits for clients, then syncs playercount, and spawns npcs
    private IEnumerator waitForClients() {
        if (this.isServer) {
            // Wait for all clients to tell the server their data file loading state.
            yield return new WaitForSeconds(1.0f);

            NetworkPlayerSelect lobbyManager = UnityEngine.Object.FindObjectOfType<NetworkPlayerSelect>();

            // When this is true, all clients are connected and in the game scene.
            // Also all clients have completed loading their data files.
            while (!GameInfo.playersReady || !lobbyManager.IsDataLoadingComplete())
                yield return 0;

            this._rngSeed = UnityEngine.Random.Range(0, 9999999);
        }
    }

    private void init(int seed) {
        this._wallTransitionUI = GameObject.Find("wallTransitionUI").GetComponent<RectTransform>();
        this._outsideWallEffect = GameObject.Find("OutsideWallEffect").GetComponent<Image>();
        this._targetWallRenderer = GameObject.Find("TargetWallMapRenderer").GetComponent<WallMapRenderer>();
        this._actualWallRenderer = GameObject.Find("FireWallMapRenderer").GetComponent<WallMapRenderer>();

        this._current = new Circle(250, Vector3.zero);
        this._target = new Circle(250, Vector3.zero);

        this._wallShrinkTimer = 0;
        this._wallIsShrinking = false;        

        this._rngSeed = seed;
        this._RNG = new System.Random(this._rngSeed);
        this.recalculateWalls();
        this._targetWallRenderer.draw(this._target.wall.transform);
        this._ready = true;
    }

    // Update is called once per frame
    void Update() {
        if (!this._ready) return;

        if (this._wallShrinkTimer > _wallShrinkTime) {
            StartCoroutine(interpolateWall());
            this._wallShrinkTimer = 0;
        }

        if (!this._wallIsShrinking) {
            this._wallShrinkTimer += Time.deltaTime;
            this.UpdateWallUI();
        }
        this._actualWallRenderer.draw(this.transform);
        spawnFire();
    } 
    
    public float getRadius() {
        return transform.localScale.x / 2;
    }

    private void spawnFire() {
        if (!WorldData.ready) return;
        if (UnityEngine.Random.Range(0.0f, 1.0f) > 0.1) return;

        Vector3 pos = WorldData.worldGrid.getRandomCell(false, 1).pos;
        
        //Check if the point is inside the firewall

        if (Vector3.Distance(pos, this._current.wall.transform.position) < getRadius()) return;
        pos.y = 10;

        RaycastHit hit;
        int layermask = (1 << 19);
        if (Physics.Raycast(pos, Vector3.down, out hit, 100, layermask)) {
            var fire = Instantiate(this._fire);
            fire.transform.position = hit.point;
            fire.transform.GetChild(0).localScale *= UnityEngine.Random.Range(0.5f, 1.5f);
            Destroy(fire, 10.0f);            
        }
    }

    private void UpdateWallUI() {
        _wallTransitionUI.anchorMax = new Vector2(this._wallShrinkTimer / _wallShrinkTime, 1);
    }

    // Calculates a new target wall, sets current wall to last target
    private void recalculateWalls() {
        Circle temp = this._current;
        this._current = this._target;
        this._target = temp;

        this._target.radius = this._current.radius / 2.0f;
        float angle = (float)_RNG.NextDouble() * Mathf.PI * 2;
        float currentWallOffset = (float)_RNG.NextDouble() * (this._current.radius - this._target.radius);
        this._target.pos = this._current.pos + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * currentWallOffset;
    }

    // Transitions the wall from current state to target state
    private IEnumerator interpolateWall() {
        float t = 0;
        this._wallIsShrinking = true;

        while (t <= 1) {
            transform.position = Vector3.Lerp(_current.wall.transform.position, _target.wall.transform.position, t);
            transform.localScale = Vector3.Lerp(_current.wall.transform.localScale, _target.wall.transform.localScale, t);

            NPCWorldView.FireWall.pos = transform.position;
            NPCWorldView.FireWall.radius = getRadius();

            t += _wallShrinkRate * Time.deltaTime;
            yield return 0;
        }
        this._wallIsShrinking = false;
        this.recalculateWalls();
        this._targetWallRenderer.draw(this._target.wall.transform);
    }

    void OnTriggerExit(Collider other) {
        if (!this._ready) return;
    
        if (other.tag == "Player" ) {
            other.GetComponent<PlayerEffects>().insideWall = false;
        }else if (other.tag == "Enemy") {
            other.GetComponent<PlayerEffects>().insideWall = false;
        } else if (other.tag == "npc") {
            other.GetComponent<NPC>().burn();
        } else if (other.tag == "DustTornado") {
            other.GetComponent<DustTornado>().kill();
        }

        if (other.tag == "bunnycamera") {
            if (other.transform.parent.tag == "Player")
                _outsideWallEffect.enabled = true;
        }
        else if (other.tag == GameObject.Find("Main Camera").GetComponent<ThirdPersonCamera>().getTargetTag()) {
            _outsideWallEffect.enabled = true;
        }
    }

    void OnTriggerEnter(Collider other) {
        if (!this._ready) return;

        if (other.tag == "Player") {
            other.GetComponent<PlayerEffects>().insideWall = true;
        } else if (other.tag == "Enemy") {
            other.GetComponent<PlayerEffects>().insideWall = true;
        }

        if (other.tag == "bunnycamera") {
            if (other.transform.parent.tag == "Player")
                _outsideWallEffect.enabled = false;
        }
        else if (other.tag == GameObject.Find("Main Camera").GetComponent<ThirdPersonCamera>().getTargetTag()) {
            _outsideWallEffect.enabled = false;
        }
    }
}