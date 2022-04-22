using Base.Audio;
using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public class Boss : EnemyBase
{
    public static event Action OnBossDefeated;

    #region States

    public enum States
    {
        Initialisation,
        Idle,
        Walking,
        Attacking
    }
    [Header("State")]
    public States state;
    public States State
    {
        get => state;
        set
        {
            if (state != value)
            {
                state = value;
                StateController(value);
            }
        }
    }

    public void StopCoroutines()
    {
        if (coroutine_Idle != null) StopCoroutine(coroutine_Idle);
        if (coroutine_Walking != null) StopCoroutine(coroutine_Walking);
        if (coroutine_Attacking != null) StopCoroutine(coroutine_Attacking);
    }

    public void StateController(States newState)
    {
        switch (newState)
        {
            case States.Idle: coroutine_Idle = StartCoroutine(State_Idle()); break;
            case States.Walking: coroutine_Walking = StartCoroutine(State_Walking()); break;
            case States.Attacking: coroutine_Attacking = StartCoroutine(State_Attacking()); break;
        }
    }

    Coroutine coroutine_Idle;
    public IEnumerator State_Idle()
    {
        while (state == States.Idle)
        {
            Movement(0);

            if (IsPlayerInAttackingRange() && CanSeePlayer())
            {
                yield return new WaitForSeconds(0.15f);
                State = States.Attacking;
                continue;
            }
            if (IsPlayerInDetectionRange() && CanSeePlayer())
            {
                yield return new WaitForSeconds(0.15f);
                State = States.Walking;
                continue;
            }


            yield return null;
        }
        coroutine_Idle = null;
    }
    Coroutine coroutine_Walking;

    public IEnumerator State_Walking()
    {
        while (state == States.Walking)
        {
            if (IsPlayerInAttackingRange())
            {
                yield return new WaitForSeconds(0.15f);
                //ForceStopMovement();
                State = States.Attacking;
                continue;
            }
            else if (!IsPlayerInDetectionRange())
            {
                yield return new WaitForSeconds(0.15f);
                State = States.Idle;
                continue;
            }

            Movement(WhatDirectionToPlayer());

            yield return null;

        }
        coroutine_Walking = null;
    }

    Coroutine coroutine_Attacking;
    public IEnumerator State_Attacking()
    {
        //Movement(0);
        //ForceStopMovement();

        while (state == States.Attacking)
        {
            if (IsPlayerInDetectionRange() && !IsPlayerInAttackingRange() && canAttack)
            {
                yield return new WaitForSeconds(0.15f);
                State = States.Walking;
                continue;
            }
            else if (!IsPlayerInDetectionRange() && !IsPlayerInAttackingRange())
            {
                yield return new WaitForSeconds(0.15f);
                State = States.Idle;
                continue;
            }

            if (canAttack)
                Attack();

            yield return null;
        }

        coroutine_Attacking = null;
    }
    #endregion States

    [Header("References")]
    public Rigidbody2D rb2D;
    public BossAnimator animator;
    PlayerController playerController;

    [Header("Detection")]
    public float detectionDistance;
    public LayerMask lineOfSightDetection;

    [Header("Combat")]
    public int damage;
    public BossDisplay bossDisplay;
    public float dashTime;
    public float dashSpeedMultiplier;
    public float attackingDistance;
    public LayerMask playerLayer;
    public float attackRate;

    [Header("Movement")]
    public float movementSpeed;

    [Header("Bool checks")]
    public bool canAttack;

    private void Awake()
    {
        rb2D = GetComponent<Rigidbody2D>();
        bossDisplay = FindObjectOfType<BossDisplay>();
    }

    private void Start()
    {
        base.Start();
        playerController = FindObjectOfType<PlayerController>();
        State = States.Idle;
    }


    public int WhatDirectionToPlayer()
    {
        if (playerController) playerController = FindObjectOfType<PlayerController>();
        Vector2 direction = playerController.transform.position - transform.position;
        return (direction.x > 0) ? 1 : -1;
    }

    public void Movement(int direction, float speedMultiplier = 1)
    {
        if (!canTakeDamage)
            return;

        float speed = (movementSpeed * speedMultiplier) * direction;
        rb2D.velocity = new Vector2(speed, rb2D.velocity.y);

        Rotation(speed);
        animator.SetEnemySpeed(Mathf.Abs(speed), movementSpeed);
    }

    public void ForceMovementWithoutMovement(float speed)
    {
        Rotation(speed);
        animator.SetEnemySpeed(Mathf.Abs(speed), speed);
    }

    public void ForceStopMovement()
    {
        rb2D.velocity = new Vector2(0, rb2D.velocity.y);

    }
    public void Rotation(float speed)
    {
        if (speed > 0)
        {
            damagePivot.localScale = new Vector2(-1, 1);
            rend.flipX = true;
        }
        else if (speed < 0)
        {
            damagePivot.localScale = new Vector2(1, 1);
            rend.flipX = false;
        }

    }

    public override void TriggerDeath()
    {
        if (hasDied) return;
        StartCoroutine(WaitBeforeFinish());
    }

    public bool hasDied;

    public IEnumerator WaitBeforeFinish()
    {
        hasDied = true;
        Movement(0);
        animator.PlayLocomotionAnimation();

        canAttack = false;
        canTakeDamage = false;

        yield return new WaitForSeconds(1.5f);
        PlayFabCustomEvents.Instance.OnBossDefeated();
        OnBossDefeated?.Invoke();
    }

    public override void DeathAnimation()
    {
        animator.PlayDeathAnimation();
    }

    public void ToggleCanAttack(bool canAttack)
    {
        this.canAttack = canAttack;
    }

    public bool CanSeePlayer()
    {
        return !Physics2D.Linecast(transform.position, playerController.transform.position, lineOfSightDetection);
    }

    public void Attack()
    {
        Movement(0);
        Rotation(WhatDirectionToPlayer());
        canAttack = false;
        animator.PlayAttackAnimation();
        AudioManager.Instance.PlaySFX("BossAttack");

    }

    public void TriggerDash()
    {
        StartCoroutine(Dash());
    }

    public IEnumerator Dash()
    {
        int direction = WhatDirectionToPlayer();

        canTakeDamage = true;

        float t = 0;
        while (t < dashTime)
        {
            t += Time.deltaTime;
            Movement(direction, dashSpeedMultiplier);
            yield return null;
        }
        Movement(0);
    }

    public override void Hitback()
    {
        canTakeDamage = false;
        Vector2 hitDirection = new Vector2(-WhatDirectionToPlayer() * hitBackStrength, hitBackHeight);
        rb2D.velocity = hitDirection;
    }

    public override int GetDamage()
    {
        return damage;
    }

    public bool IsPlayerInDetectionRange()
    {
        return Physics2D.OverlapCircle(transform.position, detectionDistance, playerLayer) != null;
    }

    public bool IsPlayerInAttackingRange()
    {
        return Physics2D.OverlapCircle(transform.position, attackingDistance, playerLayer) != null;
    }

    public override void ChangeHealthVisual(int currentHealth)
    {
        bossDisplay.healthBar.SetNewValue(currentHealth, maxHealth);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, detectionDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackingDistance);
    }


}