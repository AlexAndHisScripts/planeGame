﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using System.Security.Cryptography;
using Mirror.Examples.Basic;
using MFlight.Demo;

public class PlaneDeath : NetworkBehaviour
{
    bool DoDieNextFrame = false;
    public bool externalAuthority;
    public GameObject controller;
    public TrailRenderer trail;
    public TrailRenderer trail2;
    bool doReenableTrailsNextFrame = false;

    Quaternion startRotation;

    public GameObject deathMessage;

    float closeDeathMessageTime = 0;

    public float deathMessageShowTime;

    public void Start()
    {
        startRotation = this.transform.rotation;
        deathMessage.SetActive(false);
    }

    void Die()
    {
        if (hasAuthority == false)
        {
            Debug.Log("Not dying because I don't have authority");
            return;
        }

        DoDieNextFrame = false;
        Debug.Log("Dying");

        transform.position = new Vector3(0, 600, 0);
        transform.rotation = startRotation;

        this.GetComponent<PlaneController>().thrust = this.GetComponent<PlaneController>().startingThrust;

        doReenableTrailsNextFrame = true;

        deathMessage.SetActive(true);
        closeDeathMessageTime = Time.time + deathMessageShowTime;
    }

    public void Update()
    {
        if (Time.time > closeDeathMessageTime)
        {
            deathMessage.SetActive(false);
        }
        externalAuthority = hasAuthority;

        if (DoDieNextFrame)
        {
            Die();
        }

        if (Input.GetKeyDown(KeyCode.D))
        {
            DoDieNextFrame = true;
        }

        if (doReenableTrailsNextFrame)
        {
            trail.emitting = true;
            trail2.emitting = true;
            doReenableTrailsNextFrame = false;
        }

        if (transform.position.x > 9000 | transform.position.x < -9000 | transform.position.y < 0 | transform.position.y > 9000 | transform.position.z < -9000 | transform.position.z > 9000)
        {
            Die();
        }
    }

    void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.tag == "Bullet" | other.gameObject.tag == "Terrain")
        {
            Debug.Log("Hit by bullet");
            DoDieNextFrame = true;
            trail.emitting = false;
            trail2.emitting = false;
        }
    }
}
