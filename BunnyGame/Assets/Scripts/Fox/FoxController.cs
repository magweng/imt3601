﻿using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class FoxController : NetworkBehaviour {

    private GameObject biteArea;
    private int _biteDamage = 15;
    private bool _isAttackingAnim = false;
    

    public override void PreStartClient() {
        base.PreStartClient();
        NetworkAnimator netAnimator = GetComponent<NetworkAnimator>();

        for (int i = 0; i < GetComponent<Animator>().parameterCount; i++)
            netAnimator.SetParameterAutoSend(i, true);
    }

    // Use this for initialization
    void Start() {
        NetworkAnimator netAnimator = GetComponent<NetworkAnimator>();

        for (int i = 0; i < netAnimator.animator.parameterCount; i++)
            netAnimator.SetParameterAutoSend(i, true);

        biteArea = transform.GetChild(2).gameObject;

        if (!this.isLocalPlayer)
            return;

        // Set custom attributes for class:
        PlayerEffects pe = GetComponent<PlayerEffects>();
        pe.CmdSetAttributes(1.2f, 1.2f, 1.2f, 0.8f);

        // Add abilities to class:
        PlayerAbilityManager abilityManager = GetComponent<PlayerAbilityManager>();
        Sprint sp = gameObject.AddComponent<Sprint>();
        sp.init(50, 1);
        abilityManager.abilities.Add(sp);

        Stealth st = gameObject.AddComponent<Stealth>();
        st.init(1, 0);
        abilityManager.abilities.Add(st);

        GameObject.Find("AbilityPanel").GetComponent<AbilityPanel>().setupPanel(abilityManager);

        CmdApplySmell();
    }

    // Update is called once per frame
    void Update() {
        if (!this.isLocalPlayer)
            return;

        updateAnimator();

        if (Input.GetKeyDown(KeyCode.Mouse0))
            StartCoroutine(this.toggleBite());
            //this.bite();
    }

    private IEnumerator applySmell(int playerCount) {
        GameObject[] enemies;
        do {
            enemies = GameObject.FindGameObjectsWithTag("Enemy");
            yield return 0;
        } while (enemies.Length + 1 != playerCount);
        var smellTrail = Resources.Load<GameObject>("Prefabs/SmellTrail");
        foreach (var enemy in enemies) {
            var obj = Instantiate(smellTrail);
            obj.transform.parent = enemy.transform;
            obj.transform.localPosition = Vector3.zero;
        }
    }

    [Command]
    private void CmdApplySmell() {
        TargetApplySmell(this.connectionToClient, Object.FindObjectOfType<NetworkPlayerSelect>().numPlayers);
    }

    [TargetRpc]
    private void TargetApplySmell(NetworkConnection conn, int playerCount) {
        StartCoroutine(applySmell(playerCount));
    }

    // NB! Not needed with reverse attack logic (PlayerAttack.cs)
    /*private void bite() {
        if (this.GetComponent<PlayerHealth>().IsDead())
            return;

        if (this.isServer)
            this.RpcBite();
        else if (this.isClient)
            this.CmdBite();
    }

    [Command]
    private void CmdBite() {
        this.RpcBite();
    }

    [ClientRpc]
    private void RpcBite() {
        StartCoroutine(this.toggleBite());
    }*/

    // Biting is enabled for 1 tick after called
    private IEnumerator toggleBite() {
        _isAttackingAnim = true;
        biteArea.GetComponent<BoxCollider>().enabled = true; 
        yield return 0;
        biteArea.GetComponent<BoxCollider>().enabled = false;
        _isAttackingAnim = false;
    }

    public int GetDamage() {
        return this._biteDamage;
    }

    // Update the animator with current state
    public void updateAnimator() {
        Animator animator = GetComponent<Animator>();

        if (animator != null)
        {
            animator.SetFloat("movespeed", GetComponent<PlayerController>().currentSpeed);
            animator.SetBool("isJumping", !GetComponent<CharacterController>().isGrounded);
            animator.SetBool("isAttacking", _isAttackingAnim);
            animator.SetFloat("height", GetComponent<PlayerController>().velocityY);
        }
    }

    public Vector3 biteImpact() {
        return this.biteArea.transform.position;
    }
}
