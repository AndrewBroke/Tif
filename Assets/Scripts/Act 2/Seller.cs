using UnityEngine;

public enum SellerType
{
    None,
    Talk,
    Yell,
    Injured,
    TalkWoman
}

public class Seller : MonoBehaviour
{
    [SerializeField] SellerType sellerType;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Animator animator = GetComponent<Animator>();
        switch (sellerType)
        {
            case SellerType.Talk:
                animator.SetBool("Talk", true);
                break;
            case SellerType.Yell:
                animator.SetBool("Yell", true);
                break;
            case SellerType.Injured:
                animator.SetBool("Yell", true);
                break;
            case SellerType.TalkWoman:
                animator.SetBool("Talk", true);
                break;
        }    
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
