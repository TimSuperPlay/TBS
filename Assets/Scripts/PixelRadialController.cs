using UnityEngine;

[ExecuteInEditMode]
public class PixelRadialController : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock propertyBlock;
    [Range(0f, 1f)] public float fillAmount = 1f;

    void Update()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();

        if (spriteRenderer == null || spriteRenderer.sprite == null) return;

        Vector2 automaticTextureSize = spriteRenderer.sprite.rect.size;

        spriteRenderer.GetPropertyBlock(propertyBlock);

        propertyBlock.SetFloat("_FillAmount", fillAmount);
        propertyBlock.SetVector("_TextureSize", automaticTextureSize);

        spriteRenderer.SetPropertyBlock(propertyBlock);
    }
}
