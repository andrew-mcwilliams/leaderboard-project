import http.client
import json
import argparse
import os

def parse_args():
    parser = argparse.ArgumentParser(description='Test client for Leaderboard server')
    parser.add_argument('--json_file', type=str, help='A file contain one or more json Leaderboard API commands')
    parser.add_argument('--port', type=int, default=8080)
    parser.add_argument('--host', type=str, default='localhost')

    return parser.parse_args()

def main():
    args = parse_args()

    request_json = None

    if args.json_file is not None:
        if os.path.exists(args.json_file):
            with open(args.json_file) as f:
                request_json = json.load(f)
        else:
            print(f'JSON file does not exist:{args.json_file}')
            return

    headers = {'Content-type': 'application/json'}
    connection = http.client.HTTPConnection(f'{args.host}:{args.port}')

    for json_dict in request_json:
        if json_dict['Action'] == 'GetScores':
            connection.request('GET', '', json.dumps(json_dict), headers)
        else:
            connection.request('POST', '', json.dumps(json_dict), headers)

        response = connection.getresponse()
        print(response.read().decode())

    connection.close()

if __name__ == "__main__":
    # execute only if run as a script
    main()
