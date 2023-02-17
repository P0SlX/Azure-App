using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CloudAuthApp.Data;
using CloudAuthApp.Models;
using Azure;
using Azure.Core;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;

namespace CloudAuthApp.Controllers
{
    public class VmController : Controller
    {
        /// <summary>
        /// Instance Azure
        /// </summary>
        public Utils.Azure azure;

        /// <summary>
        /// Contexte de la base de données
        /// </summary>
        private readonly ApplicationDbContext _context;

        public VmController(ApplicationDbContext context)
        {
            // Récupération de l'instance Azure
            azure = Utils.Azure.GetInstance();

            // Assignation du contexte de la base de données
            _context = context;
        }

        // GET: Vm
        public async Task<IActionResult> Index()
        {
            // Récupération de l'utilisateur connecté et on vérifie qu'il est bien connecté
            return _context.VmModels != null
                ? View(await _context.VmModels.Where(vm => vm.Name == "vm-" + GetUserName()).ToListAsync())
                : Problem("Entity set 'ApplicationDbContext.VirtualMs'  is null.");
        }

        // GET: Vm/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            // Vérification de l'id
            if (id == null || _context.VmModels == null)
            {
                return NotFound();
            }

            // Récupération de la VM dans la base de données
            var vmModel = await _context.VmModels
                .FirstOrDefaultAsync(m => m.Id == id);

            // Not found si la VM n'existe pas
            if (vmModel == null)
            {
                return NotFound();
            }

            // Redirection vers la page de détails
            return View(vmModel);
        }

        // GET: Vm/Create
        public IActionResult Create()
        {
            // Check if user already has a VM
            if (_context.VmModels != null && _context.VmModels.Any(vm => vm.Name == "vm-" + GetUserName()))
            {
                return RedirectToAction("Index");
            }
            
            return View();
        }

        // POST: Vm/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,IpPublic,Login,Password,IsRunning")] VmModel vmModel)
        {
            // Vérification du modèle et attribution des valeurs par défaut
            string userName = GetUserName();
            vmModel.Name = "vm-" + userName;
            vmModel.IpPublic = "ip-" + userName;
            vmModel.IsRunning = true;
            ModelState.Remove("IpPublic");
            ModelState.Remove("Name");
            ModelState.Remove("IsRunning");

            // Vérification du modèle
            if (!ModelState.IsValid) return View(vmModel);

            // Récupération des ressources
            ResourceGroupResource resourceGroup = azure.GetResourceGroup();
            VirtualMachineCollection vms = azure.GetVirtualMachines();
            NetworkInterfaceCollection nics = azure.GetNetworkInterfaces();
            VirtualNetworkCollection vns = azure.GetVirtualNetworks();
            PublicIPAddressCollection publicIps = azure.GetPublicIPAddresses();

            // Récupération de l'image custom (Linux Ubuntu 18.04 LTS avec xRDP préinstallé)
            DiskImageResource customImg = azure.GetCustomImage();

            // Création de l'IP publique
            PublicIPAddressResource ipResource = publicIps.CreateOrUpdate(
                WaitUntil.Completed,
                "ip-" + userName,
                new PublicIPAddressData()
                {
                    PublicIPAddressVersion = NetworkIPVersion.IPv4,
                    PublicIPAllocationMethod = NetworkIPAllocationMethod.Dynamic,
                    Location = AzureLocation.FranceCentral
                }).Value;

            // Création du réseau virtuel
            VirtualNetworkResource vnetResrouce = vns.CreateOrUpdate(
                WaitUntil.Completed,
                "vn-" + userName,
                new VirtualNetworkData()
                {
                    Location = AzureLocation.FranceCentral,
                    Subnets =
                    {
                        new SubnetData()
                        {
                            Name = "SubNet667",
                            AddressPrefix = "10.0.0.0/24"
                        }
                    },
                    AddressPrefixes =
                    {
                        "10.0.0.0/16"
                    },
                }).Value;

            // Création de la carte réseau
            NetworkInterfaceResource nicResource = nics.CreateOrUpdate(
                WaitUntil.Completed,
                "nic-" + userName,
                new NetworkInterfaceData()
                {
                    Location = AzureLocation.FranceCentral,
                    IPConfigurations =
                    {
                        new NetworkInterfaceIPConfigurationData()
                        {
                            Name = "Primary",
                            Primary = true,
                            Subnet = new SubnetData() { Id = vnetResrouce?.Data.Subnets.First().Id },
                            PrivateIPAllocationMethod = NetworkIPAllocationMethod.Dynamic,
                            PublicIPAddress = new PublicIPAddressData() { Id = ipResource?.Data.Id }
                        }
                    }
                }).Value;

            // Création de la VM
            VirtualMachineResource vmResource = vms.CreateOrUpdate(
                WaitUntil.Completed,
                "vm-" + userName,
                new VirtualMachineData(AzureLocation.FranceCentral)
                {
                    HardwareProfile = new VirtualMachineHardwareProfile()
                    {
                        VmSize = VirtualMachineSizeType.StandardD2V3
                    },
                    OSProfile = new VirtualMachineOSProfile()
                    {
                        ComputerName = "vm-" + userName,
                        AdminUsername = vmModel.Login,
                        AdminPassword = vmModel.Password,
                        LinuxConfiguration = new LinuxConfiguration()
                        {
                            DisablePasswordAuthentication = false,
                            ProvisionVmAgent = true
                        },
                    },
                    StorageProfile = new VirtualMachineStorageProfile()
                    {
                        ImageReference = new ImageReference()
                        {
                            Id = customImg.Id
                        },
                        // Supprime le disque lors de la suppression de la VM
                        OSDisk = new VirtualMachineOSDisk(DiskCreateOptionType.FromImage)
                        {
                            DeleteOption = DiskDeleteOptionType.Delete,
                        }
                    },
                    NetworkProfile = new VirtualMachineNetworkProfile()
                    {
                        NetworkInterfaces =
                        {
                            new VirtualMachineNetworkInterfaceReference()
                            {
                                Id = nicResource.Id
                            }
                        }
                    },
                }).Value;


            // On récup l'IP après la création de la VM
            vmModel.IpPublic = InitIp(resourceGroup, "ip-" + userName).Data.IPAddress;

            // Ajout de la VM dans la base de données
            _context.Add(vmModel);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Récupère l'IP publique de la VM
        /// </summary>
        /// <param name="resourceGroup"></param>
        /// <param name="_ipName"></param>
        /// <returns></returns>
        private PublicIPAddressResource InitIp(ResourceGroupResource resourceGroup, string _ipName)
        {
            return resourceGroup.GetPublicIPAddresses().CreateOrUpdate(
                WaitUntil.Completed,
                _ipName,
                new PublicIPAddressData()
                {
                    PublicIPAddressVersion = NetworkIPVersion.IPv4,
                    PublicIPAllocationMethod = NetworkIPAllocationMethod.Dynamic,
                    Location = AzureLocation.FranceCentral
                }).Value;
        }

        // GET: Vm/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            // Vérification de l'id
            if (id == null || _context.VmModels == null)
            {
                return NotFound();
            }

            // Récupération de la VM
            var vmModel = await _context.VmModels.FindAsync(id);

            // Not found si la VM n'existe pas
            if (vmModel == null)
            {
                return NotFound();
            }

            return View(vmModel);
        }

        // POST: Vm/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,IpPublic,Login,Password")] VmModel vmModel)
        {
            // Vérification de l'id
            if (id != vmModel.Id)
            {
                return NotFound();
            }

            // Vérification du model
            if (!ModelState.IsValid) return View(vmModel);

            try
            {
                _context.Update(vmModel);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!VmModelExists(vmModel.Id))
                {
                    return NotFound();
                }

                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Vm/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.VmModels == null)
            {
                return NotFound();
            }

            var vmModel = await _context.VmModels
                .FirstOrDefaultAsync(m => m.Id == id);
            if (vmModel == null)
            {
                return NotFound();
            }

            return View(vmModel);
        }

        // Post: Vm/Start/5
        [HttpPost, ActionName("Start")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(int id)
        {
            // Récupération de la VM dans la base de données
            var vmModel = await _context.VmModels.FindAsync(id);

            // Not found si la VM n'existe pas
            if (vmModel == null)
            {
                return NotFound();
            }

            // Récupération de la VM dans Azure
            VirtualMachineResource vm = azure.GetVirtualMachine("vm-" + GetUserName());

            // Démarrage de la VM
            await vm.PowerOnAsync(WaitUntil.Completed);

            // Mise à jour de la VM dans la base de données
            vmModel.IsRunning = true;
            _context.Update(vmModel);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // Post: Vm/Stop/5
        [HttpPost, ActionName("Stop")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Stop(int id)
        {
            // Récupération de la VM dans la base de données
            var vmModel = await _context.VmModels.FindAsync(id);

            // Not found si la VM n'existe pas
            if (vmModel == null)
            {
                return NotFound();
            }

            // Récupération de la VM dans Azure
            VirtualMachineResource vm = azure.GetVirtualMachine("vm-" + GetUserName());

            // Arrêt de la VM
            await vm.PowerOffAsync(WaitUntil.Completed);

            // Mise à jour de la VM dans la base de données
            vmModel.IsRunning = false;
            _context.Update(vmModel);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // POST: Vm/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.VmModels == null) return Problem("Entity set 'ApplicationDbContext.VmModels'  is null.");

            // Récupération de la VM dans la base de données
            var vmModel = await _context.VmModels.FindAsync(id);

            // Redirection vers l'index si la VM n'existe pas
            if (vmModel == null) return RedirectToAction(nameof(Index));

            // Suppression de la VM dans la base de données
            _context.VmModels.Remove(vmModel);

            try
            {
                // Suppression de la VM et des ressources associées dans Azure
                VirtualMachineResource vm = azure.GetVirtualMachine("vm-" + GetUserName());
                NetworkInterfaceResource nic = azure.GetNetworkInterface("nic-" + GetUserName());
                VirtualNetworkResource vn = azure.GetVirtualNetwork("vn-" + GetUserName());
                PublicIPAddressResource publicIp = azure.GetPublicIPAddress("ip-" + GetUserName());

                await vm.DeleteAsync(WaitUntil.Completed, forceDeletion: true);
                await nic.DeleteAsync(WaitUntil.Completed);
                await vn.DeleteAsync(WaitUntil.Completed);
                await publicIp.DeleteAsync(WaitUntil.Completed);
            }
            catch (Exception)
            {
            }

            // Sauvegarde des changements dans la base de données
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool VmModelExists(int id)
        {
            return (_context.VmModels?.Any(e => e.Id == id)).GetValueOrDefault();
        }

        public string GetUserName()
        {
            // Récupération de l'identifiant de l'utilisateur connecté
            string? currentUserId = User.FindFirstValue(ClaimTypes.Name);

            // Vérification de l'identifiant
            if (currentUserId == null) throw new NullReferenceException();

            // Suppression des caractères spéciaux
            currentUserId = currentUserId.Split("@")[0];
            currentUserId = currentUserId.Replace(".", "");

            return currentUserId;
        }
    }
}