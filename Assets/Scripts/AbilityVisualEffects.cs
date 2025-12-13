using UnityEngine;
using System.Collections;

/// <summary>
/// Управляет визуальными эффектами и звуками при активации способностей.
/// Прикрепляется к каждому юниту для отображения эффектов способностей.
/// </summary>
public class AbilityVisualEffects : MonoBehaviour
{
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem horseJumpEffect; // Эффект для скачка коня
    [SerializeField] private ParticleSystem bishopReflectionEffect; // Эффект для отражения слона
    [SerializeField] private ParticleSystem guardianShieldEffect; // Эффект для щита ладьи
    [SerializeField] private ParticleSystem queenBoostEffect; // Эффект для усиления ферзя
    [SerializeField] private ParticleSystem kingHealEffect; // Эффект для исцеления короля
    
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource; // Источник звука
    [SerializeField] private AudioClip horseJumpSound; // Звук скачка коня
    [SerializeField] private AudioClip bishopReflectionSound; // Звук отражения слона
    [SerializeField] private AudioClip guardianShieldSound; // Звук щита ладьи
    [SerializeField] private AudioClip queenBoostSound; // Звук усиления ферзя
    [SerializeField] private AudioClip kingHealSound; // Звук исцеления короля
    
    [Header("Active Effect Indicators")]
    [SerializeField] private GameObject reflectionIndicator; // Индикатор активного отражения (слон)
    [SerializeField] private GameObject shieldIndicator; // Индикатор активного щита (ладья)
    [SerializeField] private GameObject boostIndicator; // Индикатор активного усиления (ферзь)
    
    private Unit unit;
    
    void Start()
    {
        unit = GetComponent<Unit>();
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        // Скрываем индикаторы по умолчанию
        if (reflectionIndicator != null) reflectionIndicator.SetActive(false);
        if (shieldIndicator != null) shieldIndicator.SetActive(false);
        if (boostIndicator != null) boostIndicator.SetActive(false);
    }
    
    /// <summary>
    /// Воспроизводит визуальный эффект и звук для способности коня
    /// </summary>
    public void PlayHorseJumpEffect()
    {
        if (horseJumpEffect != null)
        {
            horseJumpEffect.Play();
        }
        
        if (audioSource != null && horseJumpSound != null)
        {
            audioSource.PlayOneShot(horseJumpSound);
        }
    }
    
    /// <summary>
    /// Воспроизводит визуальный эффект и звук для способности слона
    /// </summary>
    public void PlayBishopReflectionEffect(bool isActive)
    {
        if (isActive)
        {
            if (bishopReflectionEffect != null)
            {
                bishopReflectionEffect.Play();
            }
            
            if (audioSource != null && bishopReflectionSound != null)
            {
                audioSource.PlayOneShot(bishopReflectionSound);
            }
            
            // Показываем индикатор активного эффекта
            if (reflectionIndicator != null)
            {
                reflectionIndicator.SetActive(true);
            }
        }
        else
        {
            // Скрываем индикатор когда эффект закончился
            if (reflectionIndicator != null)
            {
                reflectionIndicator.SetActive(false);
            }
        }
    }
    
    /// <summary>
    /// Воспроизводит визуальный эффект и звук для способности ладьи
    /// </summary>
    public void PlayGuardianShieldEffect(bool isActive)
    {
        if (isActive)
        {
            if (guardianShieldEffect != null)
            {
                guardianShieldEffect.Play();
            }
            
            if (audioSource != null && guardianShieldSound != null)
            {
                audioSource.PlayOneShot(guardianShieldSound);
            }
            
            // Показываем индикатор активного эффекта
            if (shieldIndicator != null)
            {
                shieldIndicator.SetActive(true);
            }
        }
        else
        {
            // Скрываем индикатор когда эффект закончился
            if (shieldIndicator != null)
            {
                shieldIndicator.SetActive(false);
            }
        }
    }
    
    /// <summary>
    /// Воспроизводит визуальный эффект и звук для способности ферзя
    /// </summary>
    public void PlayQueenBoostEffect(bool isActive)
    {
        if (isActive)
        {
            if (queenBoostEffect != null)
            {
                queenBoostEffect.Play();
            }
            
            if (audioSource != null && queenBoostSound != null)
            {
                audioSource.PlayOneShot(queenBoostSound);
            }
            
            // Показываем индикатор активного эффекта
            if (boostIndicator != null)
            {
                boostIndicator.SetActive(true);
            }
        }
        else
        {
            // Скрываем индикатор когда эффект закончился
            if (boostIndicator != null)
            {
                boostIndicator.SetActive(false);
            }
        }
    }
    
    /// <summary>
    /// Воспроизводит визуальный эффект и звук для способности короля
    /// </summary>
    public void PlayKingHealEffect(Unit targetUnit)
    {
        if (kingHealEffect != null)
        {
            // Можно создать эффект на цели исцеления
            if (targetUnit != null)
            {
                AbilityVisualEffects targetEffects = targetUnit.GetComponent<AbilityVisualEffects>();
                if (targetEffects != null && targetEffects.kingHealEffect != null)
                {
                    targetEffects.kingHealEffect.Play();
                }
            }
            else
            {
                kingHealEffect.Play();
            }
        }
        
        if (audioSource != null && kingHealSound != null)
        {
            audioSource.PlayOneShot(kingHealSound);
        }
    }
}

