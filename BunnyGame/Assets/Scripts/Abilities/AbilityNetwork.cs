﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;



/***************************************************************************
 * Used to create functions for Special Abilities that requires networking
 * 
 ***************************************************************************/


public class AbilityNetwork : NetworkBehaviour {

    private int modelChildNum = 1;

    private GameObject _poopGrenade;
    private GameObject _explosion;

    void Start() {
        this._poopGrenade = Resources.Load<GameObject>("Prefabs/PoopGrenade/PoopGrenade");
        this._explosion = Resources.Load<GameObject>("Prefabs/PoopGrenade/PoopExplosion");
    }
  

    ///////////// Functions for Stealth ability /////////////////

    public void useStealth(int modelChildNum, float activeTime, float transparancy)
    {
        this.modelChildNum = modelChildNum;
        CmdStealth(activeTime, transparancy);
    }
    
    [Command]
    public void CmdStealth(float activeTime, float transparancy)
    {
        StartCoroutine(stealth(activeTime,transparancy));
    }

    private IEnumerator stealth(float activeTime,float transparancy)
    {
        RpcSetTransparentFox(transparancy);
        yield return new WaitForSeconds(activeTime);
        RpcSetOrginalFox();
    }

    [ClientRpc]
    private void RpcSetTransparentFox(float transparancy)
    {
        Material[] materials;
        Color alfa;
      
        foreach (Transform child in this.transform.GetChild(modelChildNum))
        {
			if(child.gameObject.GetComponent<Renderer>() != null)
				materials = child.gameObject.GetComponent<Renderer>().materials;
			else if (child.gameObject.GetComponent<SkinnedMeshRenderer>() != null)
				materials = child.gameObject.GetComponent<SkinnedMeshRenderer>().materials;
			else
				continue;

			int count = 0;
            foreach (Material mat in materials)
            {
				alfa = mat.color;
                alfa.a = transparancy;
                materials[count++].SetColor("_Color", alfa);

                mat.renderQueue = 3100;
            }
        }
    }

    [ClientRpc]
    public void RpcSetOrginalFox()
    {
        Material[] materials;
        Color alfa;
        float orginal = 1.0f;
        
        foreach (Transform child in this.transform.GetChild(modelChildNum)) {
			if (child.gameObject.GetComponent<Renderer>() != null)
				materials = child.gameObject.GetComponent<Renderer>().materials;
			else if (child.gameObject.GetComponent<SkinnedMeshRenderer>() != null)
				materials = child.gameObject.GetComponent<SkinnedMeshRenderer>().materials;
			else
				continue;
			int count = 0;
            foreach (Material mat in materials) {
				alfa = mat.color;
                alfa.a = orginal;
                mat.renderQueue = 2000;
                materials[count++].SetColor("_Color", alfa);
			}
        }
    }
    /////////////////////////////////////////////////////////////////

    ///////////// Functions for GrenadePoop ability /////////////////


    [Command]
    public void CmdPoopGrenade(Vector3 direction, Vector3 startVel, int id) {
        GameObject poop = Instantiate(this._poopGrenade);
        GrenadePoopProjectile poopScript = poop.GetComponent<GrenadePoopProjectile>();
        Vector3 position = (transform.position + direction * 5.0f);

        poopScript.ConnectionID = id;   // Assign the player connection ID to the projectile.
        poopScript.shoot(direction, position, startVel);
        poopScript.owner = this.gameObject;

        NetworkServer.Spawn(poop);
    }

    [Command]
    public void CmdPoopExplosion(Vector3 pos) {
        GameObject explosion = Instantiate(this._explosion);
        explosion.transform.position = pos;
        NetworkServer.Spawn(explosion);
        Destroy(explosion, 1.1f);
    }
    /////////////////////////////////////////////////////////////////
}