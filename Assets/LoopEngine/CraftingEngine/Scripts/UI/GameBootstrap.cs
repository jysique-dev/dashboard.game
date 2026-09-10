using LoopEngine.CraftingEngine;
using LoopEngine.CraftingEngine.Samples;
using LoopEngine.CraftingEngine.UI;
using LoopEngine.TagInput;
using System.Linq;
using UnityEngine;

public sealed class GameBootstrap : MonoBehaviour
{
   
    [SerializeField] private CraftingDatabase _database;
    [SerializeField] private CraftingUIHost craft_ui;
    [SerializeField] private InventoryUIHost inventory_ui;
    [SerializeField] private CraftingTestBed _testBed;

    private void Awake()
    {
        _testBed.OnOpenCrafting += BindCrafting;
        _testBed.OnOpenInventory += BindInventory;
    }
       

    void BindCrafting(CraftingSystem _system)
    {
        craft_ui.Bind(_system);

    }
    void BindInventory(IItemContainer _container)
    {
        inventory_ui.Bind(_container);
    }

    private void Update()
    {        
        if (KeyInputManager.Down("open_mac")) OpenMachine(_testBed.Machine);

        if (KeyInputManager.Down("close_mac")) CloseMachine();


        //if (KeyInputManager.Down("open_inv")) OpenInventory();
        //if (KeyInputManager.Down("close_inv")) CloseInventory();
    }

    public void OpenMachine(MachineInstance machine) => craft_ui.Show(machine);
    public void CloseMachine() => craft_ui.Hide();

    //public void OpenInventory()=> inventory_ui.SetVisible(true);
    //public void CloseInventory()=> inventory_ui.SetVisible(false);
}