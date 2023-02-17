using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Resources;

namespace CloudAuthApp.Utils;

/// <summary>
/// Classe responsable de la connexion à l'API Azure
/// </summary>
public class Azure
{
    /// <summary>
    /// Client de l'API Azure
    /// </summary>
    private ArmClient client;
    
    /// <summary>
    /// Instance de la classe Azure
    /// </summary>
    private static Azure instance;
    
    /// <summary>
    /// Souscription Azure
    /// </summary>
    private SubscriptionResource subscription;
    
    /// <summary>
    ///  Liste de groupe de ressources Azure
    /// </summary>
    private ResourceGroupCollection resourceGroups;
    
    /// <summary>
    /// Groupe de ressources Azure
    /// </summary>
    private ResourceGroupResource resourceGroup;
    
    /// <summary>
    /// Image contenant Ubuntu 18.04 LTS avec xRDP préinstallé
    /// </summary>
    private DiskImageResource customImg;

    public Azure()
    {
        // Connexion à l'API Azure avec les identifiants de l'application
        client = new ArmClient(new ClientSecretCredential("", "", ""));
        
        // Récupération de la souscription et du groupe de ressources
        subscription = client.GetSubscriptions().Get("");
        resourceGroups = subscription.GetResourceGroups();
        resourceGroup = resourceGroups.Get("rg-gaming-667");
        customImg = resourceGroup.GetDiskImages().Get("img-vm-linux");
    }

    /// <summary>
    /// Retourne l'instance de la classe Azure
    /// </summary>
    /// <returns></returns>
    public static Azure GetInstance()
    {
        if (Azure.instance == null)
        {
            Azure.instance = new Azure();
        }

        return Azure.instance;
    }

    public SubscriptionResource GetSubscription()
    {
        return GetInstance().subscription;
    }

    public ResourceGroupCollection GetResourceGroups()
    {
        return GetInstance().resourceGroups;
    }

    public ResourceGroupResource GetResourceGroup()
    {
        return GetInstance().resourceGroup;
    }

    public VirtualMachineCollection GetVirtualMachines()
    {
        return GetResourceGroup().GetVirtualMachines();
    }

    public VirtualMachineResource GetVirtualMachine(string name)
    {
        return GetVirtualMachines().Get(name);
    }

    public NetworkInterfaceCollection GetNetworkInterfaces()
    {
        return GetResourceGroup().GetNetworkInterfaces();
    }

    public VirtualNetworkCollection GetVirtualNetworks()
    {
        return GetResourceGroup().GetVirtualNetworks();
    }

    public PublicIPAddressCollection GetPublicIPAddresses()
    {
        return GetResourceGroup().GetPublicIPAddresses();
    }

    public DiskImageResource GetCustomImage()
    {
        return GetInstance().customImg;
    }

    public NetworkInterfaceResource GetNetworkInterface(string name)
    {
        return GetNetworkInterfaces().Get(name);
    }

    public VirtualNetworkResource GetVirtualNetwork(string name)
    {
        return GetVirtualNetworks().Get(name);
    }

    public PublicIPAddressResource GetPublicIPAddress(string name)
    {
        return GetPublicIPAddresses().Get(name);
    }
}
