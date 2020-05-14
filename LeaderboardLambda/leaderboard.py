from tinydb import TinyDB, where
from operator import itemgetter
import argparse
import json
import importlib
import time
from http.server import HTTPServer, BaseHTTPRequestHandler
from functools import partial


# a class to take the json from a http request and convert it into an object with members we can access
# for example we can access {'key', 'data'} like request.key
class LeaderboardRequest:
    def __init__(self, *json_data, **kwargs):
        for dict in json_data:
            for key in dict:
                setattr(self, key, dict[key])

        for key in kwargs:
            setattr(self, key, kwargs[key])


# base class for handling requests
# each class name is mapped directly to the 'Action' parameter in the API
# ideally this sort of thing would be code generated to ensure we don't end up with silly typo failures
class RequestHandler:
    def HandleRequest(self, request, db):
        pass


class SubmitScore(RequestHandler):
    def create_submit_score_response(self, success, error_string=None):
        response = {'Success': success, 'ErrorString': error_string}

        return response

    def HandleRequest(self, request, db):
        if not hasattr(request, 'Username') or request.Username is None or request.Username.isspace():
            return self.create_submit_score_response(False, 'SubmitScore must provide a Username')
        if not hasattr(request, 'Score') or request.Score is None:
            return self.create_submit_score_response(False, 'SubmitScore must provide a Score')

        user_scores = db.search(where('Username') == request.Username)

        request_timestamp = getattr(request, 'Timestamp', time.time())

        if len(user_scores):
            # this user has previous entries, let's make sure the most recent one isn't newer than the requested one
            recent_entry = LeaderboardRequest(user_scores[-1])

            if recent_entry.Timestamp >= request_timestamp:
                return self.create_submit_score_response(False, f'Score could not be submitted. A record with a '
                                                                f'timestamp greater than or equal to '
                                                                f'{request_timestamp} already exists.')

        new_user_score = {'Username': request.Username, 'Score': request.Score, 'Timestamp': request_timestamp}

        # todo: check if the insertion actually succeeded and there were no errors in the db
        db.insert(new_user_score)

        return self.create_submit_score_response(True)


class GetScores(RequestHandler):
    def __init__(self):
        self.default_max_entries = 10

    def create_get_scores_response(self, success, scores, error_string=None):
        response = {'Success': success, 'Scores': scores, 'ErrorString': error_string}
        return response

    def HandleRequest(self, request, db):
        user_name = None if not hasattr(request, 'Username') or request.Username.isspace() else request.Username
        max_entries = self.default_max_entries if not hasattr(request, 'MaxEntries') else request.MaxEntries
        timestamp_range = None if not hasattr(request, 'TimestampRange') else request.TimestampRange

        if max_entries <= 0 or max_entries is None:
            return self.create_get_scores_response(False, [], 'MaxEntries must be a number greater than zero.')

        order = 'Descending' if not hasattr(request, 'Order') or request.Order.isspace() else request.Order

        # build up the query based on the parameters we've received
        query_list = []

        if user_name:
            query_list.append(where('Username') == user_name)
        if timestamp_range:
            if 'TimestampBegin' not in timestamp_range or 'TimestampEnd' not in timestamp_range:
                return self.create_get_scores_response(False, [], 'TimestampRange requires both TimestampBegin and '
                                                                  'TimestampEnd be provided.')

            query_list.append((where('Timestamp') >= timestamp_range['TimestampBegin']) &
                              (where('Timestamp') <= timestamp_range['TimestampEnd']))

        if len(query_list):
            GetScores = query_list[0]
            for query in query_list[1:]:
                GetScores = GetScores & query

            user_scores = db.search(GetScores)
        else:
            user_scores = db.all()

        if not len(user_scores):
            # the error message here could be more detailed, including which parameters don't match
            return self.create_get_scores_response(False, [], 'No records were found matching the provided parameters.')

        user_scores = sorted(user_scores, key=itemgetter('Score'), reverse=True if order == 'Descending' else False)
        user_scores = user_scores[:max_entries]

        return self.create_get_scores_response(True, user_scores)


class DeleteScore(RequestHandler):
    def create_delete_score_response(self, success, deletion_count, error_string=None):
        response = {'Success': success, 'DeletedCount': deletion_count, 'ErrorString': error_string}
        return response

    def HandleRequest(self, request, db):
        if not hasattr(request, 'Username') or request.Username is None or request.Username.isspace():
            return self.create_delete_score_response(False, 0, 'DeleteScore must provide a Username')

        timestamp_range = None if not hasattr(request, 'TimestampRange') else request.TimestampRange

        DeleteScore = where('Username') == request.Username
        if timestamp_range:
            if 'TimestampBegin' not in timestamp_range or 'TimestampEnd' not in timestamp_range:
                return self.create_delete_score_response(False, 0, 'TimestampRange requires both TimestampBegin and '
                                                                   'TimestampEnd be provided.')
            DeleteScore = DeleteScore & \
                          ((where('Timestamp') >= timestamp_range['TimestampBegin']) &
                           (where('Timestamp') <= timestamp_range['TimestampEnd']))

        deleted_scores = db.remove(DeleteScore)
        if not len(deleted_scores):
            return self.create_delete_score_response(False, 0, 'No records matched the provided parameters. No '
                                                               'records were deleted.')

        return self.create_delete_score_response(True, len(deleted_scores))


class ClearScores(RequestHandler):
    def create_clear_scores_response(self, success, deletion_count, error_string=None):
        response = {'Success': success, 'DeletedCount': deletion_count, 'ErrorString': error_string}
        return response

    def HandleRequest(self, request, db):
        timestamp_range = None if not hasattr(request, 'TimestampRange') else request.TimestampRange

        if timestamp_range:
            if 'TimestampBegin' not in timestamp_range or 'TimestampEnd' not in timestamp_range:
                return self.create_clear_scores_response(False, 0, 'TimestampRange requires both TimestampBegin and '
                                                                   'TimestampEnd be provided.')
            deleted_scores = db.remove((where('Timestamp') >= timestamp_range['TimestampBegin'])
                                       & (where('Timestamp') <= timestamp_range['TimestampEnd']))
        else:
            deleted_scores = db.remove(where('Timestamp') >= 0)

        if not len(deleted_scores):
            return self.create_clear_scores_response(False, 0, 'No records matched the provided parameters. No '
                                                               'records were deleted.')

        return self.create_clear_scores_response(True, len(deleted_scores))


def parse_args():
    parser = argparse.ArgumentParser(description='Local support of Leaderboard Lambda "server"')
    parser.add_argument('--database_path', type=str, default='database.json',
                        help='Path to the local TinyDB json database')

    return parser.parse_args()


def initialize_tinydb(database_path):
    # ideally there's be error handling around database/table creation here
    # tinydb will just create a new file if it doesn't already exist
    # but in a more realistic scenario we want to point at "the" db, not just any old db we create willy nilly
    return TinyDB(database_path).table('leaderboard')


host = "localhost"
port = 8080


class LeaderboardHTTPRequestHandler(BaseHTTPRequestHandler):
    def __init__(self, db, *args, **kwargs):
        self.db = db

        request_handlers = {}

        # create our request handlers from any class that inherits from RequestHandler
        current_module = importlib.import_module(__name__)
        for subclass in RequestHandler.__subclasses__():
            class_def = getattr(current_module, subclass.__name__)  # get the definition of the class from the module
            class_instance = class_def()  # instantiate the class
            request_handlers[subclass.__name__] = class_instance

        self.request_handlers = request_handlers

        # apparently the BaseHandler here calls do_GET, do_POST,etc inside its init......
        super().__init__(*args, **kwargs)

    def get_incorrect_content_type_response(self, expected_content_type):
        response = {'Success': False, 'ErrorString': 'Content-Type expected to be application/json. Got '
                                                     f'{expected_content_type}'}
        return response

    def call_request_handler(self, request):
        leaderboard_request = LeaderboardRequest(request)

        if not hasattr(leaderboard_request, 'Action') or leaderboard_request.Action is None:
            self.send_response(400)
            response = {'Success': False, 'ErrorString':'Requests must contain an Action'}
        else:
            if leaderboard_request.Action not in self.request_handlers:
                self.send_response(400)
                response = {'Success': False, 'ErrorString': f'Unknown Action {leaderboard_request.Action}'}
            else:
                response = self.request_handlers[leaderboard_request.Action].HandleRequest(leaderboard_request, self.db)
                if response['Success']:
                    self.send_response(200)
                else:
                    self.send_response(400) # 400 isn't always right here, the handlers should return what failure happened

        self.send_header('Content-type', 'application/json')
        self.end_headers()
        self.wfile.write(json.dumps(response).encode())

    def verify_headers(self):
        content_type = self.headers.get('Content-Type')
        if 'application/json' not in content_type:
            self.send_response(400)
            self.send_header('Content-type', 'application/json')
            self.wfile.write(json.dumps(self.get_incorrect_content_type_response('application/json')).encode())
            self.end_headers()
            return False

        content_length = self.headers.get('Content-Length')
        if content_length is None:
            self.send_response(400)
            self.end_headers()
            return False

        return True

    def handle_request(self):
        try:
            if not self.verify_headers():
                return

            content_length = int(
                self.headers.get('Content-Length'))  # we're just going to assume content-length is an int
            request = json.loads(self.rfile.read(content_length))
            self.call_request_handler(request)
        except json.decoder.JSONDecodeError as jde:  # let them know that the json was malformed
            self.send_response(400)
            response = {'Success': False, 'ErrorString': f'JSONDecodeError: {str(jde)}'}

            self.send_header('Content-type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps(response).encode())

        except Exception as e:  # otherwise log the exception locally and let them know the request failed internally
            print(e)
            self.send_response(500)
            response = {'Success': False, 'ErrorString': 'Internal Server Error'}

            self.send_header('Content-type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps(response).encode())

    def do_GET(self):
        self.handle_request()

    def do_POST(self):
        self.handle_request()


def main():
    args = parse_args()

    db = initialize_tinydb(args.database_path)

    handler = partial(LeaderboardHTTPRequestHandler, db)

    with HTTPServer((host, port), handler) as httpd:
        try:
            httpd.serve_forever()
        except KeyboardInterrupt:
            pass


if __name__ == "__main__":
    # execute only if run as a script
    main()
