import json
import re
import requests
import os

# Function to split PascalCase into Title Case
def split_pascal_case(name):
    if name.upper() == name:
        return name
    return re.sub(r'(?<!^)(?=[A-Z])', ' ', name)

# Function to create a slug from a string for use in Markdown links
def slugify(name):
    return name.lower().replace(' ', '-')

# Define the groups for quantity types
# These groups are used to organize the generated documentation
# Any quantity not explicitly listed here will be added to the "Other" category.
quantity_groups = {
    "Basic Dimensions": [
        "AmountOfSubstance", "Angle", "Area", "Duration", "Information",
        "Length", "Mass", "Scalar", "SolidAngle", "Volume"
    ],
    "Mechanics": [
        "Acceleration", "BrakeSpecificFuelConsumption", "Compressibility", "Density",
        "DynamicViscosity", "Force", "ForceChangeRate", "ForcePerLength",
        "FuelEfficiency", "Impulse", "Jerk", "KinematicViscosity", "LinearDensity",
        "MassFlow", "MassFlux", "MassFraction", "MassMomentOfInertia", "Pressure",
        "PressureChangeRate", "RotationalAcceleration", "RotationalSpeed",
        "RotationalStiffness", "RotationalStiffnessPerLength", "SpecificFuelConsumption",
        "SpecificVolume", "SpecificWeight", "Speed", "StandardVolumeFlow",
        "Torque", "TorquePerLength", "VolumeFlow", "VolumeFlowPerArea", "VolumePerLength",
        "WarpingMomentOfInertia"
    ],
    "Electrical & Magnetic": [
        "ElectricAdmittance", "ElectricApparentEnergy", "ElectricApparentPower",
        "ElectricCapacitance", "ElectricCharge", "ElectricChargeDensity",
        "ElectricConductance", "ElectricConductivity", "ElectricCurrent",
        "ElectricCurrentDensity", "ElectricCurrentGradient", "ElectricField",
        "ElectricImpedance", "ElectricInductance", "ElectricPotential",
        "ElectricPotentialChangeRate", "ElectricReactance", "ElectricReactiveEnergy",
        "ElectricReactivePower", "ElectricResistance", "ElectricResistivity",
        "ElectricSurfaceChargeDensity", "ElectricSusceptance", "MagneticField",
        "MagneticFlux", "Magnetization", "Permeability", "Permittivity"
    ],
    "Thermal": [
        "CoefficientOfThermalExpansion", "Energy", "EnergyDensity", "Entropy",
        "HeatFlux", "HeatTransferCoefficient", "MolarEnergy", "MolarEntropy",
        "SpecificEnergy", "SpecificEntropy", "Temperature", "TemperatureChangeRate",
        "TemperatureDelta", "TemperatureGradient", "ThermalConductivity",
        "ThermalResistance", "VolumetricHeatCapacity"
    ],
    "Light & Radiation": [
        "AbsorbedDoseOfIonizingRadiation", "DoseAreaProduct", "Illuminance",
        "Irradiance", "Irradiation", "Luminance", "Luminosity", "LuminousFlux",
        "LuminousIntensity", "RadiationEquivalentDose", "RadiationEquivalentDoseRate",
        "RadiationExposure", "Radioactivity"
    ],
    "Chemical & Material": [
        "Molality", "Molarity", "PorousMediumPermeability", "Turbidity", "MassConcentration", "VolumeConcentration"
    ],
    "Ratios & Levels": [
        "AmplitudeRatio", "Level", "Ratio", "RatioChangeRate", "RelativeHumidity"
    ]
}

# We read the units from the UnitsNet repository
# and dynamically categorize quantities that are not explicitly defined in the quantity_groups.
UNITS_JSON_URL = 'https://raw.githubusercontent.com/angularsen/UnitsNet/master/Common/UnitEnumValues.g.json'
UNIT_DEFINITION_BASE_URL = 'https://raw.githubusercontent.com/angularsen/UnitsNet/master/Common/UnitDefinitions/'
LOCAL_UNITS_JSON_PATH = 'Units.json' 
OUTPUT_MD_PATH = 'Units.md' 
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
SCRIPT_NAME = 'generate_units_doc.py'

print(f"Generating \033[36m{OUTPUT_MD_PATH}\033[0m with the list of supported units of measure")
print(f"Source of truth: \033[36m{UNITS_JSON_URL}\033[0m")
print(f"Source of truth: \033[36m{UNIT_DEFINITION_BASE_URL}\033[0m\n\n")

# Download Units.json
print(f"Downloading and processing the list...")
response = requests.get(UNITS_JSON_URL)
response.raise_for_status()
with open(LOCAL_UNITS_JSON_PATH, 'w', encoding='utf-8') as f:
    f.write(response.text)

# Read the local Units.json file
with open(LOCAL_UNITS_JSON_PATH, 'r', encoding='utf-8') as f:
    content = f.read()

# Strip comments and parse JSON
start_index = content.find('{')
json_content = content[start_index:]
data = json.loads(json_content)

# Dynamically categorize quantities not explicitly defined in quantity_groups
uncategorized_quantities = []
existing_quantities = set()
for group_quantities in quantity_groups.values():
    existing_quantities.update(group_quantities)

for q_name in data.keys():
    if q_name not in existing_quantities:
        uncategorized_quantities.append(q_name)

if uncategorized_quantities:
    if "Other" not in quantity_groups:
        quantity_groups["Other"] = []
    quantity_groups["Other"].extend(sorted(uncategorized_quantities))

# Generate the Markdown content
print(f"Starting to generate \033[36m{OUTPUT_MD_PATH}\033[0m...")
with open(OUTPUT_MD_PATH, 'w', encoding='utf-8') as f:
    f.write('# Supported Units\n\n')
    f.write('This document lists all the quantity types and their corresponding units of measurement. It is generated from UnitsNet package JSON data.\n\n')
    
    print("Rendering table of contents...")
    f.write('## Table of Contents\n\n')
    for group_name in quantity_groups.keys():
        f.write(f'- [{group_name}](#{slugify(group_name)})\n')
        for quantity_name in sorted(quantity_groups[group_name]):
            f.write(f'  - [{split_pascal_case(quantity_name)}](#{slugify(split_pascal_case(quantity_name))})\n')
    f.write('\n')

    for group_name, quantities_in_group in quantity_groups.items():
        print(f"Rendering group \033[36m{group_name}\033[0m...")
        f.write(f'## {group_name}\n\n')
        for quantity_name in sorted(quantities_in_group):
            try:
                try:
                    url = f'{UNIT_DEFINITION_BASE_URL}{quantity_name}.json'
                    print(f"Downloading units data from \033[36mhttps://raw.githubusercontent.com/.../{quantity_name}.json\033[0m...")
                    response = requests.get(url)
                    response.raise_for_status()
                    unit_def = json.loads(response.content.decode('utf-8-sig'))
                except requests.RequestException as e:
                    print(f"\033[33mFailed ({e.__class__.__name__}) to gather additional information for \033[36m{quantity_name}\033[0m")
                    unit_def = {
                        "Units": {}
                    }

                f.write(f'### {split_pascal_case(quantity_name)}\n`{quantity_name}`\n\n')
                if unit_def.get('XmlDocSummary'):
                    f.write(f'{unit_def["XmlDocSummary"]}\n\n')
                if unit_def.get('BaseUnit'):
                    f.write(f'**Default Unit**: {unit_def["BaseUnit"]}\n\n')

                f.write('**Units**:\n\n')
                for unit in unit_def['Units']:
                    unit_name = unit['SingularName']
                    
                    en_us_abbrs = []
                    if 'Localization' in unit:
                        for loc_entry in unit['Localization']:
                            if 'Culture' in loc_entry and loc_entry['Culture'] == 'en-US':
                                if 'Abbreviations' in loc_entry:
                                    en_us_abbrs.extend(loc_entry['Abbreviations'])
                                break
                    
                    abbr_str = ', '.join(en_us_abbrs)

                    f.write(f'- {split_pascal_case(unit_name)} (`{unit_name}`)')
                    if abbr_str:
                        f.write(f'.\n\n  Abbreviation(s): {abbr_str}')
                    if unit.get('XmlDocSummary'):
                        f.write(f'\n\n  {unit["XmlDocSummary"]}')
                    f.write('\n')
                f.write('\n')
            except (requests.exceptions.RequestException, json.JSONDecodeError) as e:
                print(f"\033[33mCould not process \033[36m{quantity_name}\033[0m: {e}")

print(f"\033[32mSuccessfully generated \033[36m{OUTPUT_MD_PATH}\033[0m")

# Instructions for use:
# 1. Make sure you have Python installed.
# 2. Install the 'requests' library: pip install requests
# 3. Run this script from your terminal: python generate_units_doc.py
# 4. The Units.md file will be generated in the same directory as this script.