using UnityEngine;
using System.Collections;

public class Plant : MonoBehaviour
{
    public PlantData plantData; // **确保变量在类内**
    private bool isCollectable = false;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isCollectable = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isCollectable = false;
        }
    }

    void Update()
    {
        if (isCollectable && Input.GetKeyDown(KeyCode.J))
        {
            CollectPlant();
        }
    }

    void CollectPlant()
    {
        PlayerHealth playerHealth = GameObject.FindWithTag("Player")?.GetComponent<PlayerHealth>();
        PlayerEvolution playerEvolution = GameObject.FindWithTag("Player")?.GetComponent<PlayerEvolution>();
        PlayerMovement playerMovement = GameObject.FindWithTag("Player")?.GetComponent<PlayerMovement>();

        if (playerHealth != null)
        {
            playerHealth.Heal(plantData.healAmount);
            Debug.Log($"玩家采集了 {plantData.plantName}，恢复 {plantData.healAmount} 生命值！");
        }

        if (playerEvolution != null)
        {
            playerEvolution.AddEvolutionPoints(plantData.evolutionPoints);
            Debug.Log($"玩家获得 {plantData.evolutionPoints} 进化点数！");
        }

        if (playerMovement != null)
        {
            playerMovement.Stun(1f);
        }

        StartCoroutine(RespawnPlant());
        gameObject.SetActive(false);
    }

    IEnumerator RespawnPlant()
    {
        yield return new WaitForSeconds(10f);
        gameObject.SetActive(true);
    }
}
