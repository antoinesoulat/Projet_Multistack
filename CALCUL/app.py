from flask import Flask, request, jsonify
import requests
from flask_cors import CORS
import logging
import json

app = Flask(__name__)
CORS(app)

logging.basicConfig(level=logging.DEBUG)

def est_pair(nombre):
    return nombre % 2 == 0

def est_parfait(nombre):
    diviseurs = [i for i in range(1, nombre) if nombre % i == 0]
    return sum(diviseurs) == nombre

def est_premier(nombre):
    if nombre < 2:
        return False
    for i in range(2, int(nombre**0.5) + 1):
        if nombre % i == 0:
            return False
    return True

def suite_syracuse(nombre):
    suite = [nombre]
    while nombre != 1:
        nombre = nombre // 2 if nombre % 2 == 0 else 3 * nombre + 1
        suite.append(nombre)
    return suite

DOTNET_API_URL_VERIFY = "http://bdd:8000/api/number/verify-id"
DOTNET_API_URL_INSERT = "http://bdd:8000/api/number/insert"

@app.route('/calculate', methods=['POST'])
def calculate():
    data = request.get_json()
    nombre = data.get('nombre')

    if nombre is None or not isinstance(nombre, int):
        return jsonify({"error": "Entrez un nombre entier valide"}), 400

    try:
        response = requests.get(f"{DOTNET_API_URL_VERIFY}?nombre={nombre}")
        response.raise_for_status()
        result = response.json()
        app.logger.info(result)

        if result.get('exists', False):
            app.logger.info(f"Le nombre {nombre} existe déjà. Données MySQL : {result}")
            return jsonify(result)  

        result = {
            "Nombre": nombre,
            "EstPair": est_pair(nombre),
            "EstParfait": est_parfait(nombre),
            "EstPremier": est_premier(nombre),
        }

        syracuse = {
            "Nombre": nombre,
            "SuiteSyracuse": suite_syracuse(nombre),
        }

        combined_data = {
            "mysqlData": result,
            "syracuseData": syracuse
        }

        app.logger.info(f"Envoi des données combinées à l'API .NET: {json.dumps(combined_data, indent=4)}")

        insert_response = requests.post(DOTNET_API_URL_INSERT, json=combined_data)
        insert_response.raise_for_status()

        insert_result = insert_response.json()
        app.logger.info(f"Résultat inséré retourné par l'API .NET : {json.dumps(insert_result, indent=4)}")

        return jsonify(insert_result)

    except requests.RequestException as e:
        app.logger.error(f"Erreur lors de l'insertion via l'API .NET: {e}")
        if e.response:
            app.logger.error(f"Statut HTTP de l'erreur : {e.response.status_code}")
            app.logger.error(f"Contenu brut de l'erreur : {e.response.text}")
        return jsonify({"error": "Erreur lors de l'insertion via l'API .NET", "details": str(e)}), 500



if __name__ == '__main__':
    app.run(debug=True, host='backend', port=5001)
